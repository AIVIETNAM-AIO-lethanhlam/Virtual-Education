// firebase_server_test.js

const express = require("express");
const cors = require("cors");
const multer = require("multer");
const path = require("path");
const { admin, db, bucket } = require("./config/firebase");
const fs = require("fs");
const os = require("os");
const pdfPoppler = require("pdf-poppler");
const sharp = require("sharp");

// ===============================
// FIREBASE CONFIG
// ===============================
// Đổi tên file service account theo file của bạn

const app = express();

app.use(cors());
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

const upload = multer({
    storage: multer.memoryStorage(),
    fileFilter: function (req, file, cb) {
        if (file.mimetype === "application/pdf") {
            cb(null, true);
        } else {
            cb(new Error("Only PDF files are allowed"));
        }
    },
    limits: {
        fileSize: 100 * 1024 * 1024
    }
});

async function convertPdfToQuestionImages(pdfBuffer, quizId, classId, lessonId) {
    const pdfjsLib = await import("pdfjs-dist/legacy/build/pdf.mjs");

    const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), "quiz_pdf_"));
    const pdfPath = path.join(tempDir, "quiz.pdf");

    fs.writeFileSync(pdfPath, pdfBuffer);

    await pdfPoppler.convert(pdfPath, {
        format: "png",
        out_dir: tempDir,
        out_prefix: "page",
        page: null
    });

    const pageImages = fs
        .readdirSync(tempDir)
        .filter(file => file.endsWith(".png"))
        .sort((a, b) => {
            const na = Number(a.match(/\d+/)?.[0] || 0);
            const nb = Number(b.match(/\d+/)?.[0] || 0);
            return na - nb;
        });

    const loadingTask = pdfjsLib.getDocument({
        data: new Uint8Array(pdfBuffer)
    });

    const pdfDoc = await loadingTask.promise;

    const questions = [];
    let globalQuestionOrder = 1;

    for (let pageIndex = 0; pageIndex < pdfDoc.numPages; pageIndex++) {
        const page = await pdfDoc.getPage(pageIndex + 1);
        const viewport = page.getViewport({ scale: 1 });

        const textContent = await page.getTextContent();

        const linesMap = new Map();

        for (const item of textContent.items) {
            const text = item.str.trim();
            if (!text) continue;

            const x = item.transform[4];
            const y = item.transform[5];

            const key = Math.round(y);

            if (!linesMap.has(key)) {
                linesMap.set(key, {
                    y,
                    text: "",
                    items: []
                });
            }

            const line = linesMap.get(key);
            line.items.push({ x, text });
        }

        const lines = Array.from(linesMap.values())
            .map(line => {
                line.items.sort((a, b) => a.x - b.x);
                line.text = line.items.map(i => i.text).join(" ").trim();
                return line;
            })
            .sort((a, b) => b.y - a.y);

        const questionStarts = [];
        const answers = [];

        for (let i = 0; i < lines.length; i++) {
            const lineText = lines[i].text;

            const questionMatch = lineText.match(/^(Câu|Question)\s*\d+\s*[:.]/i);

            const answerMatch = lineText.match(
                /(Đáp\s*án|Answer|Correct\s*Answer|Ans)\s*[:：]\s*([A-D])/i
            );

            if (questionMatch) {
                questionStarts.push({
                    lineIndex: i,
                    y: lines[i].y,
                    text: lineText
                });
            }

            if (answerMatch) {
                answers.push({
                    lineIndex: i,
                    y: lines[i].y,
                    correct_answer: answerMatch[2].toUpperCase()
                });
            }
        }

        if (questionStarts.length === 0) continue;

        const pageImagePath = path.join(tempDir, pageImages[pageIndex]);
        const metadata = await sharp(pageImagePath).metadata();

        const imageWidth = metadata.width;
        const imageHeight = metadata.height;

        const scaleY = imageHeight / viewport.height;

        for (let i = 0; i < questionStarts.length; i++) {
            const currentQuestion = questionStarts[i];
            const nextQuestion = questionStarts[i + 1];

            const relatedAnswer = answers.find(answer => {
                const afterQuestion = answer.lineIndex > currentQuestion.lineIndex;
                const beforeNextQuestion = !nextQuestion || answer.lineIndex < nextQuestion.lineIndex;
                return afterQuestion && beforeNextQuestion;
            });

            const correctAnswer = relatedAnswer ? relatedAnswer.correct_answer : "";

            let top = Math.floor((viewport.height - currentQuestion.y) * scaleY) - 20;

            let bottom;

            if (relatedAnswer) {
                bottom = Math.floor((viewport.height - relatedAnswer.y) * scaleY) - 10;
            } else if (nextQuestion) {
                bottom = Math.floor((viewport.height - nextQuestion.y) * scaleY) - 20;
            } else {
                bottom = imageHeight - 20;
            }

            top = Math.max(0, top);
            bottom = Math.min(imageHeight, bottom);

            const cropHeight = bottom - top;

            if (cropHeight <= 30) {
                console.warn("Bỏ qua câu hỏi vì cropHeight quá nhỏ:", currentQuestion.text);
                continue;
            }

            const cropBuffer = await sharp(pageImagePath)
                .extract({
                    left: 0,
                    top: top,
                    width: imageWidth,
                    height: cropHeight
                })
                .png()
                .toBuffer();

            const questionOrder = globalQuestionOrder;
            const questionId = `question_${questionOrder}`;

            const storagePath =
                `quiz_questions/${classId}/${lessonId}/${quizId}/${questionId}.png`;

            const imageFile = bucket.file(storagePath);

            await imageFile.save(cropBuffer, {
                metadata: {
                    contentType: "image/png"
                }
            });

            await imageFile.makePublic();

            const imageUrl =
                `https://storage.googleapis.com/${bucket.name}/${storagePath}`;

            questions.push({
                question_id: questionId,
                question_order: questionOrder,
                question_image_url: imageUrl,
                correct_answer: correctAnswer,
                page_number: pageIndex + 1,
                created_at: new Date()
            });

            globalQuestionOrder++;
        }
    }

    fs.rmSync(tempDir, {
        recursive: true,
        force: true
    });

    return questions;
}

// ===============================
// HEALTH CHECK
// ===============================
app.get("/", (req, res) => {
    res.json({
        success: true,
        message: "Firebase test server is running"
    });
});

// ===============================
// GET LESSON DETAIL
// Dùng cho CreateExerciseScene / DoExerciseScene
// ===============================
app.get("/api/classes/:classId/lessons/:lessonId", async (req, res) => {
    try {
        const { classId, lessonId } = req.params;

        const lessonDoc = await db.collection("lessons").doc(lessonId).get();

        if (!lessonDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy bài học."
            });
        }

        const lesson = lessonDoc.data();

        if (lesson.class_id !== classId) {
            return res.status(400).json({
                success: false,
                message: "Bài học không thuộc lớp học này."
            });
        }

        res.json({
            success: true,
            lesson: {
                id: lessonDoc.id,
                ...lesson
            }
        });
    } catch (error) {
        res.status(500).json({
            success: false,
            message: "Lỗi khi lấy thông tin bài học.",
            error: error.message
        });
    }
});

// ===============================
// UPLOAD QUIZ PDF
// Dùng cho CreateExerciseScene_test
// ===============================
app.post(
    "/api/classes/:classId/lessons/:lessonId/upload-quiz-pdf",
    upload.single("quiz_pdf"),
    async (req, res) => {
        try {
            const { classId, lessonId } = req.params;
            const deadlineDate = req.body.deadline_date || "";
            const deadlineTime = req.body.deadline_time || "";

            if (!req.file) {
                return res.status(400).json({
                    success: false,
                    message: "Vui lòng chọn file PDF."
                });
            }

            const classDoc = await db.collection("classes").doc(classId).get();

            if (!classDoc.exists) {
                return res.status(404).json({
                    success: false,
                    message: "Không tìm thấy lớp học."
                });
            }

            const lessonRef = db.collection("lessons").doc(lessonId);
            const lessonDoc = await lessonRef.get();

            if (!lessonDoc.exists) {
                return res.status(404).json({
                    success: false,
                    message: "Không tìm thấy bài học."
                });
            }

            const lessonData = lessonDoc.data();

            if (lessonData.class_id !== classId) {
                return res.status(400).json({
                    success: false,
                    message: "Bài học này không thuộc lớp học đã chọn."
                });
            }

            const originalName = Buffer
                .from(req.file.originalname, "latin1")
                .toString("utf8");

            const safeFileName = originalName
                .normalize("NFD")
                .replace(/[\u0300-\u036f]/g, "")
                .replace(/[^a-zA-Z0-9._-]/g, "_");

            const storagePath = `quiz_pdfs/${classId}/${lessonId}/${Date.now()}_${safeFileName}`;

            const file = bucket.file(storagePath);

            await file.save(req.file.buffer, {
                metadata: {
                    contentType: req.file.mimetype
                }
            });

            await file.makePublic();

            const quizPdfUrl = `https://storage.googleapis.com/${bucket.name}/${storagePath}`;

            const quizRef = db.collection("quizzes").doc();

            const questions = await convertPdfToQuestionImages(
                req.file.buffer,
                quizRef.id,
                classId,
                lessonId
            );

            const questionsMetaPath =
                `quiz_questions/${classId}/${lessonId}/${quizRef.id}/questions`;

            const questionsMetaFile = bucket.file(questionsMetaPath);

            const questionsMetaData = {
                success: true,
                quiz_id: quizRef.id,
                class_id: classId,
                lesson_id: lessonId,
                total_questions: questions.length
            };

            await questionsMetaFile.save(JSON.stringify(questionsMetaData), {
                metadata: {
                    contentType: "application/json"
                }
            });

            await questionsMetaFile.makePublic();

            const now = new Date();

            const quizData = {
                quiz_id: quizRef.id,
                class_id: classId,
                lesson_id: lessonId,
                teacher_id: lessonData.teacher_id || "",

                quiz_pdf_url: quizPdfUrl,
                quiz_pdf_name: originalName || req.file.originalname,

                open_time: now,
                deadline_date: deadlineDate,
                deadline_time: deadlineTime,

                total_questions: questions.length,
                question_images: questions,

                created_at: now,
                updated_at: null
            };

            for (const question of questions) {
                await quizRef
                    .collection("questions")
                    .doc(question.question_id)
                    .set(question);
            }

            await lessonRef.update({
                quiz_id: quizRef.id,
                exercise_pdf_url: quizPdfUrl,
                deadline_date: deadlineDate,
                deadline_time: deadlineTime,
                updated_at: now
            });

            console.log("Created quiz id:", quizRef.id);
            console.log("Updated lesson quiz_id:", quizRef.id);

            res.json({
                success: true,
                message: "Upload quiz PDF thành công.",
                quiz: quizData
            });

        } catch (error) {
            console.error("Upload quiz PDF error:", error);

            res.status(500).json({
                success: false,
                message: "Lỗi server khi upload quiz PDF.",
                error: error.message
            });
        }
    }
);

// ===============================
// GET QUIZ BY LESSON
// Dùng cho DoExerciseScene_test
// ===============================
app.get("/api/classes/:classId/lessons/:lessonId/quiz", async (req, res) => {
    try {
        const { classId, lessonId } = req.params;

        const lessonRef = db.collection("lessons").doc(lessonId);
        const lessonDoc = await lessonRef.get();

        if (!lessonDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy bài học."
            });
        }

        const lessonData = lessonDoc.data();

        if (lessonData.class_id !== classId) {
            return res.status(400).json({
                success: false,
                message: "Bài học không thuộc lớp học này."
            });
        }

        let quizDoc = null;
        let quizData = null;

        // 1. Ưu tiên lấy quiz theo lesson.quiz_id
        if (lessonData.quiz_id) {
            const tempQuizDoc = await db
                .collection("quizzes")
                .doc(lessonData.quiz_id)
                .get();

            if (tempQuizDoc.exists) {
                quizDoc = tempQuizDoc;
                quizData = tempQuizDoc.data();
            }
        }

        // 2. Nếu lesson.quiz_id bị cũ/sai thì tìm quiz mới nhất theo class_id + lesson_id
        if (!quizDoc) {
            const quizSnapshot = await db
                .collection("quizzes")
                .where("class_id", "==", classId)
                .where("lesson_id", "==", lessonId)
                .get();

            if (quizSnapshot.empty) {
                return res.status(404).json({
                    success: false,
                    message: "Không tìm thấy quiz."
                });
            }

            let latestDoc = null;
            let latestTime = 0;

            quizSnapshot.forEach((doc) => {
                const data = doc.data();

                let time = 0;

                if (data.created_at && data.created_at.toDate) {
                    time = data.created_at.toDate().getTime();
                }

                if (!latestDoc || time > latestTime) {
                    latestDoc = doc;
                    latestTime = time;
                }
            });

            quizDoc = latestDoc;
            quizData = latestDoc.data();

            // Update lại lesson.quiz_id cho đúng quiz mới nhất
            await lessonRef.update({
                quiz_id: quizDoc.id,
                exercise_pdf_url: quizData.quiz_pdf_url || "",
                updated_at: new Date()
            });
        }

        return res.json({
            success: true,
            quiz: {
                id: quizDoc.id,

                quiz_id: quizData.quiz_id || quizDoc.id,
                class_id: quizData.class_id || classId,
                lesson_id: quizData.lesson_id || lessonId,
                teacher_id: quizData.teacher_id || lessonData.teacher_id || "",

                quiz_pdf_url: quizData.quiz_pdf_url || "",
                quiz_pdf_name: quizData.quiz_pdf_name || "",

                open_time: formatFirestoreDate(quizData.open_time || quizData.created_at),
                deadline_date: quizData.deadline_date || lessonData.deadline_date || "",
                deadline_time: quizData.deadline_time || lessonData.deadline_time || "",

                total_questions: quizData.total_questions || 0,
                question_images: quizData.question_images || [],

                created_at: formatFirestoreDate(quizData.created_at),
                updated_at: formatFirestoreDate(quizData.updated_at)
            }
        });

    } catch (error) {
        console.error("Get quiz by lesson error:", error);

        return res.status(500).json({
            success: false,
            message: "Lỗi khi lấy quiz.",
            error: error.message
        });
    }
});

// ===============================
// GET LESSONS BY CLASS
// Dùng cho LessonInClassManager.cs
// URL cũ từ MySQL: /api/lessons/class/:classId
// ===============================
app.get("/api/lessons/class/:classId", async (req, res) => {
    try {
        const classId = req.params.classId;

        if (!classId) {
            return res.status(400).json({
                success: false,
                message: "Thiếu classId."
            });
        }

        const classDoc = await db.collection("classes").doc(classId).get();

        if (!classDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy lớp học."
            });
        }

        const snapshot = await db
            .collection("lessons")
            .where("class_id", "==", classId)
            .get();

        const lessons = [];

        snapshot.forEach((doc) => {
            const data = doc.data();

            lessons.push({
                lesson_id: doc.id,
                class_id: data.class_id || "",
                teacher_id: data.teacher_id || "",
                lesson_title: data.lesson_title || "",
                lesson_info: data.lesson_info || "",
                lesson_img: data.lesson_img || "",
                lesson_img_url: data.lesson_img_url || "",
                lesson_pdf_url: data.lesson_pdf_url || "",
                exercise_pdf_url: data.exercise_pdf_url || "",
                quiz_id: data.quiz_id || "",
                deadline_date: data.deadline_date || "",
                deadline_time: data.deadline_time || "",
                time_exercise: data.time_exercise || "",
                created_at: data.created_at || null,
                updated_at: data.updated_at || null
            });
        });

        return res.json({
            success: true,
            lessons: lessons
        });

    } catch (error) {
        console.error("Get lessons by class error:", error);

        return res.status(500).json({
            success: false,
            message: "Lỗi server khi lấy danh sách bài học.",
            error: error.message
        });
    }
});

// ===============================
// GET QUIZ QUESTION IMAGES
// Dùng cho QuizDoingPanel
// ===============================
// app.get("/api/quizzes/:quizId/questions", async (req, res) => {
//     try {
//         const { quizId } = req.params;

//         const quizDoc = await db.collection("quizzes").doc(quizId).get();

//         if (!quizDoc.exists) {
//             return res.status(404).json({
//                 success: false,
//                 message: "Không tìm thấy quiz."
//             });
//         }

//         const snapshot = await db
//             .collection("quizzes")
//             .doc(quizId)
//             .collection("questions")
//             .orderBy("question_order", "asc")
//             .get();

//         const questions = [];

//         snapshot.forEach((doc) => {
//             const data = doc.data();

//             questions.push({
//                 question_id: data.question_id || doc.id,
//                 question_order: data.question_order || 0,
//                 question_image_url: data.question_image_url || "",
//                 correct_answer: data.correct_answer || ""
//             });
//         });

//         return res.json({
//             success: true,
//             quiz_id: quizId,
//             total_questions: questions.length,
//             questions: questions
//         });

//     } catch (error) {
//         console.error("Get quiz questions error:", error);

//         return res.status(500).json({
//             success: false,
//             message: "Lỗi khi lấy danh sách câu hỏi.",
//             error: error.message
//         });
//     }
// });

// ===============================
// GET QUIZ QUESTIONS BY QUIZ ID
// Trả về total_questions + danh sách questions
// ===============================
app.get("/api/quizzes/:quizId/questions", async (req, res) => {
    try {
        const { quizId } = req.params;

        if (!quizId) {
            return res.status(400).json({
                success: false,
                message: "Thiếu quizId."
            });
        }

        const quizRef = db.collection("quizzes").doc(quizId);
        const quizDoc = await quizRef.get();

        if (!quizDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy quiz."
            });
        }

        const quizData = quizDoc.data();

        const snapshot = await quizRef
            .collection("questions")
            .get();

        const questions = [];

        snapshot.forEach((doc) => {
            const data = doc.data();

            questions.push({
                question_id: data.question_id || doc.id,
                question_order: Number(data.question_order || 0),
                question_image_url: data.question_image_url || "",
                correct_answer: data.correct_answer || ""
            });
        });

        questions.sort((a, b) => a.question_order - b.question_order);

        return res.json({
            success: true,
            quiz_id: quizId,
            class_id: quizData.class_id || "",
            lesson_id: quizData.lesson_id || "",
            total_questions: questions.length,
            questions: questions
        });

    } catch (error) {
        console.error("Get quiz questions error:", error);

        return res.status(500).json({
            success: false,
            message: "Lỗi khi lấy danh sách câu hỏi.",
            error: error.message
        });
    }
});



// ===============================
// SERVER START
// ===============================
const PORT = 4000;

app.listen(PORT, () => {
    console.log(`Firebase test server is running at http://localhost:${PORT}`);
});