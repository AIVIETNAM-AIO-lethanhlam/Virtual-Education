const express = require("express");
const multer = require("multer");
const admin = require("firebase-admin");
const axios = require("axios");
const { db, bucket } = require("./config/firebase");

const fs = require("fs");
const path = require("path");
const os = require("os");
const sharp = require("sharp");
const pdfPoppler = require("pdf-poppler");

const { execFile } = require("child_process");

const pdfParse = require("pdf-parse");
const https = require("https");
const ffmpeg = require("fluent-ffmpeg");
const ffmpegInstaller = require("@ffmpeg-installer/ffmpeg");
ffmpeg.setFfmpegPath(ffmpegInstaller.path); // Khởi tạo FFmpeg
const app = express();


app.use(express.json());
app.use(express.urlencoded({ extended: true }));

app.use("/uploads", express.static("uploads"));



function clampExtractArea(area, imageWidth, imageHeight) {
    if (
        !area ||
        !Number.isFinite(area.left) ||
        !Number.isFinite(area.top) ||
        !Number.isFinite(area.width) ||
        !Number.isFinite(area.height) ||
        !Number.isFinite(imageWidth) ||
        !Number.isFinite(imageHeight)
    ) {
        return null;
    }

    let left = Math.floor(area.left);
    let top = Math.floor(area.top);
    let width = Math.floor(area.width);
    let height = Math.floor(area.height);

    left = Math.max(0, left);
    top = Math.max(0, top);

    if (left >= imageWidth || top >= imageHeight) {
        return null;
    }

    width = Math.min(width, imageWidth - left);
    height = Math.min(height, imageHeight - top);

    if (width < 1 || height < 1) {
        return null;
    }

    return {
        left: left,
        top: top,
        width: width,
        height: height
    };
}
// ---------------------- Firebase Storage Upload Config ----------------------

const uploadPdf = multer({
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

async function uploadFileToFirebase(file, folderName) {
    const originalName = file.originalname || "file.pdf";

    let ext = path.extname(originalName).toLowerCase();

    if (!ext) {
        if (file.mimetype === "application/pdf") {
            ext = ".pdf";
        } else if (file.mimetype && file.mimetype.includes("png")) {
            ext = ".png";
        } else if (file.mimetype && file.mimetype.includes("jpeg")) {
            ext = ".jpg";
        } else if (file.mimetype && (file.mimetype.includes("audio/mpeg") || file.mimetype.includes("audio/mp3"))) {
            ext = ".mp3"; 
        } else if (file.mimetype && file.mimetype.includes("video/mp4")) {
            ext = ".mp4"; 
        } else if (file.mimetype && file.mimetype.includes("video/mp5")) {
            ext = ".mp5"; 
        } else if (file.mimetype && file.mimetype.includes("video")) {
            ext = ".mp4"; 
        } else {
            ext = ".bin";
        }
    }

    const safeFileName = `${Date.now()}-${Math.random().toString(36).substring(2, 8)}${ext}`;
    const fileName = `${folderName}/${safeFileName}`;

    const firebaseFile = bucket.file(fileName);

    await firebaseFile.save(file.buffer, {
        metadata: {
            contentType: file.mimetype
        }
    });

    await firebaseFile.makePublic();

    const fileUrl = `https://storage.googleapis.com/${bucket.name}/${encodeURI(fileName)}`;

    return {
        file_name: fileName,
        file_url: fileUrl
    };
}
async function convertAndUploadVideoToFirebase(file, folderName) {
    return new Promise((resolve, reject) => {
        // Tạo thư mục tạm để xử lý video
        const tempDir = path.join(__dirname, "temp_videos");
        if (!fs.existsSync(tempDir)) {
            fs.mkdirSync(tempDir, { recursive: true });
        }

        const inputPath = path.join(tempDir, `input_${Date.now()}_${Math.random().toString(36).substring(7)}.mp4`);
        const outputPath = path.join(tempDir, `output_${Date.now()}_${Math.random().toString(36).substring(7)}.mp4`);

        // Ghi file người dùng upload ra máy chủ tạm
        fs.writeFileSync(inputPath, file.buffer);

        console.log(" Video sang chuẩn Unity...");

        ffmpeg(inputPath)
    .outputOptions([
        '-c:v libx264',
        '-profile:v baseline',   // Fix: H.264 timestamp issue
        '-level 3.0',
        '-pix_fmt yuv420p',      // Fix: Color primaries unknown
        '-vf scale=trunc(iw/2)*2:trunc(ih/2)*2',  // Fix: width/height phải chia hết cho 2
        '-movflags +faststart',  // Fix: Unity có thể stream qua HTTP (moov atom lên đầu)
        '-c:a aac',
        '-b:a 128k',
        '-r 30'                  // Normalize framerate
    ])
            .save(outputPath)
            .on('end', async () => {
                try {
                    console.log("Convert thành công! Đang up lên Firebase...");
                    // Đọc file đã convert xong
                    const convertedBuffer = fs.readFileSync(outputPath);
                    
                    const safeFileName = `${Date.now()}-${Math.random().toString(36).substring(2, 8)}.mp4`;
                    const fileName = `${folderName}/${safeFileName}`;
                    const firebaseFile = bucket.file(fileName);

                    await firebaseFile.save(convertedBuffer, {
                        metadata: { contentType: "video/mp4" }
                    });
                    await firebaseFile.makePublic();

                    const fileUrl = `https://storage.googleapis.com/${bucket.name}/${encodeURI(fileName)}`;

                    // Xóa rác (file tạm) sau khi up xong
                    fs.unlinkSync(inputPath);
                    fs.unlinkSync(outputPath);

                    resolve({ file_name: fileName, file_url: fileUrl });
                } catch (err) {
                    reject(err);
                }
            })
            .on('error', (err) => {
                console.error("Lỗi FFmpeg Convert:", err);
                if (fs.existsSync(inputPath)) fs.unlinkSync(inputPath);
                if (fs.existsSync(outputPath)) fs.unlinkSync(outputPath);
                reject(err);
            });
    });
}
async function downloadPdfFromFirebaseStorage(fileName, outputPath) {
    if (!fileName) {
        throw new Error("Missing Firebase Storage file name");
    }

    await bucket.file(fileName).download({
        destination: outputPath
    });

    console.log("Downloaded PDF from Firebase Storage:", fileName);
}

// app.get("/test-firestore", async (req, res) => {
//     try {

//         await db.collection("test").add({
//             message: "Firebase connected successfully",
//             created_at: new Date()
//         });

//         res.json({
//             success: true,
//             message: "Connected to Firestore successfully"
//         });

//     } catch (error) {

//         console.error(error);

//         res.status(500).json({
//             success: false,
//             error: error.message
//         });
//     }
// });

// ----------------- Transform PDF to Image for Firebase ------------------------
const uploadQuizPdf = multer({
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



// async function convertPdfToQuestionImages(pdfBuffer, quizId, classId, lessonId) {
//     const pdfjsLib = await import("pdfjs-dist/legacy/build/pdf.mjs");

//     const tempDir = fs.mkdtempSync(path.join(os.tmpdir(), "quiz_pdf_"));
//     const pdfPath = path.join(tempDir, "quiz.pdf");

//     fs.writeFileSync(pdfPath, pdfBuffer);

//     await pdfPoppler.convert(pdfPath, {
//         format: "png",
//         out_dir: tempDir,
//         out_prefix: "page",
//         page: null
//     });

//     const pageImages = fs
//         .readdirSync(tempDir)
//         .filter(file => file.endsWith(".png"))
//         .sort((a, b) => {
//             const na = Number(a.match(/\d+/)?.[0] || 0);
//             const nb = Number(b.match(/\d+/)?.[0] || 0);
//             return na - nb;
//         });

//     const loadingTask = pdfjsLib.getDocument({
//         data: new Uint8Array(pdfBuffer)
//     });

//     const pdfDoc = await loadingTask.promise;

//     const questions = [];
//     let globalQuestionOrder = 1;

//     for (let pageIndex = 0; pageIndex < pdfDoc.numPages; pageIndex++) {
//         const page = await pdfDoc.getPage(pageIndex + 1);
//         const viewport = page.getViewport({ scale: 1 });
//         const textContent = await page.getTextContent();

//         const linesMap = new Map();

//         for (const item of textContent.items) {
//             const text = item.str.trim();
//             if (!text) continue;

//             const x = item.transform[4];
//             const y = item.transform[5];
//             const key = Math.round(y);

//             if (!linesMap.has(key)) {
//                 linesMap.set(key, {
//                     y,
//                     text: "",
//                     items: []
//                 });
//             }

//             linesMap.get(key).items.push({ x, text });
//         }

//         const lines = Array.from(linesMap.values())
//             .map(line => {
//                 line.items.sort((a, b) => a.x - b.x);
//                 line.text = line.items.map(i => i.text).join(" ").trim();
//                 return line;
//             })
//             .sort((a, b) => b.y - a.y);

//         const questionStarts = [];
//         const answers = [];

//         for (let i = 0; i < lines.length; i++) {
//             const lineText = lines[i].text;

//             const questionMatch = lineText.match(/^(Câu|Question)\s*\d+\s*[:.]/i);

//             const answerMatch = lineText.match(
//                 /(Đáp\s*án|Answer|Correct\s*Answer|Ans)\s*[:：]\s*([A-D])/i
//             );

//             if (questionMatch) {
//                 questionStarts.push({
//                     lineIndex: i,
//                     y: lines[i].y,
//                     text: lineText
//                 });
//             }

//             if (answerMatch) {
//                 answers.push({
//                     lineIndex: i,
//                     y: lines[i].y,
//                     correct_answer: answerMatch[2].toUpperCase()
//                 });
//             }
//         }

//         if (questionStarts.length === 0) continue;

//         const pageImagePath = path.join(tempDir, pageImages[pageIndex]);
//         const metadata = await sharp(pageImagePath).metadata();

//         const imageWidth = metadata.width;
//         const imageHeight = metadata.height;
//         const scaleY = imageHeight / viewport.height;

//         for (let i = 0; i < questionStarts.length; i++) {
//             const currentQuestion = questionStarts[i];
//             const nextQuestion = questionStarts[i + 1];

//             const relatedAnswer = answers.find(answer => {
//                 const afterQuestion = answer.lineIndex > currentQuestion.lineIndex;
//                 const beforeNextQuestion = !nextQuestion || answer.lineIndex < nextQuestion.lineIndex;
//                 return afterQuestion && beforeNextQuestion;
//             });

//             const correctAnswer = relatedAnswer ? relatedAnswer.correct_answer : "";

//             let top = Math.floor((viewport.height - currentQuestion.y) * scaleY) - 20;
//             let bottom;

//             if (relatedAnswer) {
//                 bottom = Math.floor((viewport.height - relatedAnswer.y) * scaleY) - 10;
//             } else if (nextQuestion) {
//                 bottom = Math.floor((viewport.height - nextQuestion.y) * scaleY) - 20;
//             } else {
//                 bottom = imageHeight - 20;
//             }

//             top = Math.max(0, top);
//             bottom = Math.min(imageHeight, bottom);

//             const cropHeight = bottom - top;

//             if (cropHeight <= 30) {
//                 console.warn("Bỏ qua câu hỏi vì cropHeight quá nhỏ:", currentQuestion.text);
//                 continue;
//             }

//             // const cropBuffer = await sharp(pageImagePath)
//             //     .extract({
//             //         left: 0,
//             //         top: top,
//             //         width: imageWidth,
//             //         height: cropHeight
//             //     })
//             //     .png()
//             //     .toBuffer();

//             const safeArea = clampExtractArea(
//                 {
//                     left: 0,
//                     top: top,
//                     width: imageWidth,
//                     height: cropHeight
//                 },
//                 imageWidth,
//                 imageHeight
//             );

//             if (safeArea == null) {
//                 console.warn("Bỏ qua câu hỏi vì vùng crop không hợp lệ:", {
//                     question: currentQuestion.text,
//                     top: top,
//                     bottom: bottom,
//                     cropHeight: cropHeight,
//                     imageWidth: imageWidth,
//                     imageHeight: imageHeight
//                 });

//                 continue;
//             }

//             console.log("Crop safeArea =", safeArea);

//             let cropBuffer = await sharp(pageImagePath)
//                 .extract(safeArea)
//                 .png()
//                 .toBuffer();

//             try {
//                 cropBuffer = await sharp(cropBuffer)
//                     .trim({
//                         background: "#ffffff",
//                         threshold: 15
//                     })
//                     .extend({
//                         top: 20,
//                         bottom: 20,
//                         left: 20,
//                         right: 20,
//                         background: "#ffffff"
//                     })
//                     .png()
//                     .toBuffer();
//             } catch (trimError) {
//                 console.warn("Trim ảnh thất bại, dùng ảnh crop gốc:", trimError.message);
//             }

//             const questionOrder = globalQuestionOrder;
//             const questionId = `question_${questionOrder}`;

//             const storagePath =
//                 `quiz_questions/${classId}/${lessonId}/${quizId}/${questionId}.png`;

//             const imageFile = bucket.file(storagePath);

//             await imageFile.save(cropBuffer, {
//                 metadata: {
//                     contentType: "image/png"
//                 }
//             });

//             await imageFile.makePublic();

//             const imageUrl =
//                 `https://storage.googleapis.com/${bucket.name}/${storagePath}`;

//             questions.push({
//                 question_id: questionId,
//                 question_order: questionOrder,
//                 question_image_url: imageUrl,
//                 correct_answer: correctAnswer,
//                 page_number: pageIndex + 1,
//                 created_at: new Date()
//             });

//             globalQuestionOrder++;
//         }
//     }

//     fs.rmSync(tempDir, {
//         recursive: true,
//         force: true
//     });

//     return questions;
// }

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

    const pageInfos = [];
    const allLines = [];

    for (let pageIndex = 0; pageIndex < pdfDoc.numPages; pageIndex++) {
        const page = await pdfDoc.getPage(pageIndex + 1);
        const viewport = page.getViewport({ scale: 1 });
        const textContent = await page.getTextContent();

        const pageImagePath = path.join(tempDir, pageImages[pageIndex]);
        const metadata = await sharp(pageImagePath).metadata();

        const pageInfo = {
            pageIndex: pageIndex,
            pageImagePath: pageImagePath,
            viewportHeight: viewport.height,
            imageWidth: metadata.width,
            imageHeight: metadata.height,
            scaleY: metadata.height / viewport.height
        };

        pageInfos.push(pageInfo);

        const linesMap = new Map();

        for (const item of textContent.items) {
            const text = item.str.trim();
            if (!text) continue;

            const x = item.transform[4];
            const y = item.transform[5];
            const key = Math.round(y);

            if (!linesMap.has(key)) {
                linesMap.set(key, {
                    y: y,
                    text: "",
                    items: []
                });
            }

            linesMap.get(key).items.push({ x, text });
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
            allLines.push({
                globalIndex: allLines.length,
                pageIndex: pageIndex,
                lineIndexInPage: i,
                y: lines[i].y,
                text: lines[i].text
            });
        }
    }

    const questionStarts = [];
    const answers = [];

    for (let i = 0; i < allLines.length; i++) {
        const line = allLines[i];

        const questionMatch = line.text.match(/^(Câu|Question)\s*\d+\s*[:.]/i);

        const answerMatch = line.text.match(
            /(Đáp\s*án|Answer|Correct\s*Answer|Ans)\s*[:：]\s*([A-D])/i
        );

        if (questionMatch) {
            questionStarts.push(line);
        }

        if (answerMatch) {
            answers.push({
                ...line,
                correct_answer: answerMatch[2].toUpperCase()
            });
        }
    }

    const questions = [];

    for (let qIndex = 0; qIndex < questionStarts.length; qIndex++) {
        const currentQuestion = questionStarts[qIndex];
        const nextQuestion = questionStarts[qIndex + 1];

        const relatedAnswer = answers.find(answer => {
            const afterQuestion = answer.globalIndex > currentQuestion.globalIndex;
            const beforeNextQuestion = !nextQuestion || answer.globalIndex < nextQuestion.globalIndex;
            return afterQuestion && beforeNextQuestion;
        });

        const endLine = relatedAnswer || nextQuestion || null;
        const correctAnswer = relatedAnswer ? relatedAnswer.correct_answer : "";

        const startPageIndex = currentQuestion.pageIndex;
        const endPageIndex = endLine ? endLine.pageIndex : startPageIndex;

        const cropParts = [];

        for (let pageIndex = startPageIndex; pageIndex <= endPageIndex; pageIndex++) {
            const pageInfo = pageInfos[pageIndex];

            let top = 0;
            let bottom = pageInfo.imageHeight;

            if (pageIndex === startPageIndex) {
                top = Math.floor((pageInfo.viewportHeight - currentQuestion.y) * pageInfo.scaleY) - 20;
            }

            if (pageIndex === endPageIndex && endLine) {
                bottom = Math.floor((pageInfo.viewportHeight - endLine.y) * pageInfo.scaleY) - 10;
            }

            top = Math.max(0, top);
            bottom = Math.min(pageInfo.imageHeight, bottom);

            const cropHeight = bottom - top;

            if (cropHeight <= 30) {
                console.warn("Bỏ qua crop part vì quá nhỏ:", {
                    question: currentQuestion.text,
                    pageIndex: pageIndex + 1,
                    top,
                    bottom,
                    cropHeight
                });
                continue;
            }

            const safeArea = clampExtractArea(
                {
                    left: 0,
                    top: top,
                    width: pageInfo.imageWidth,
                    height: cropHeight
                },
                pageInfo.imageWidth,
                pageInfo.imageHeight
            );

            if (safeArea == null) {
                console.warn("Bỏ qua crop part vì vùng crop không hợp lệ:", {
                    question: currentQuestion.text,
                    pageIndex: pageIndex + 1,
                    top,
                    bottom,
                    cropHeight,
                    imageWidth: pageInfo.imageWidth,
                    imageHeight: pageInfo.imageHeight
                });
                continue;
            }

            let partBuffer = await sharp(pageInfo.pageImagePath)
                .extract(safeArea)
                .png()
                .toBuffer();

            try {
                partBuffer = await sharp(partBuffer)
                    .trim({
                        background: "#ffffff",
                        threshold: 15
                    })
                    .extend({
                        top: 10,
                        bottom: 10,
                        left: 20,
                        right: 20,
                        background: "#ffffff"
                    })
                    .png()
                    .toBuffer();
            } catch (trimError) {
                console.warn("Trim crop part thất bại:", trimError.message);
            }

            cropParts.push(partBuffer);
        }

        if (cropParts.length === 0) {
            console.warn("Không crop được câu hỏi:", currentQuestion.text);
            continue;
        }

        let finalBuffer;

        if (cropParts.length === 1) {
            finalBuffer = cropParts[0];
        } else {
            const partMetas = [];

            for (const part of cropParts) {
                const meta = await sharp(part).metadata();
                partMetas.push(meta);
            }

            const finalWidth = Math.max(...partMetas.map(m => m.width));
            const finalHeight = partMetas.reduce((sum, m) => sum + m.height, 0);

            let currentTop = 0;
            const composites = [];

            for (let i = 0; i < cropParts.length; i++) {
                // composites.push({
                //     input: cropParts[i],
                //     left: Math.floor((finalWidth - partMetas[i].width) / 2),
                //     top: currentTop
                // });
                composites.push({
                    input: cropParts[i],
                    left: 0,
                    top: currentTop
                });

                currentTop += partMetas[i].height;
            }

            finalBuffer = await sharp({
                create: {
                    width: finalWidth,
                    height: finalHeight,
                    channels: 3,
                    background: "#ffffff"
                }
            })
                .composite(composites)
                .png()
                .toBuffer();
        }

        const questionOrder = questions.length + 1;
        const questionId = `question_${questionOrder}`;

        const storagePath =
            `quiz_questions/${classId}/${lessonId}/${quizId}/${questionId}.png`;

        const imageFile = bucket.file(storagePath);

        await imageFile.save(finalBuffer, {
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
            page_number: startPageIndex + 1,
            created_at: new Date()
        });
    }

    fs.rmSync(tempDir, {
        recursive: true,
        force: true
    });

    return questions;
}

function formatFirestoreDate(value) {
    if (!value) return "";

    try {
        if (value.toDate && typeof value.toDate === "function") {
            return value.toDate().toLocaleString("vi-VN", {
                timeZone: "Asia/Ho_Chi_Minh"
            });
        }

        if (value instanceof Date) {
            return value.toLocaleString("vi-VN", {
                timeZone: "Asia/Ho_Chi_Minh"
            });
        }

        if (typeof value === "string") {
            return value;
        }

        return "";
    } catch (error) {
        console.error("Format date error:", error);
        return "";
    }
}




// ------------------------- Register ---------------------
app.post("/api/register", async (req, res) => {
    try {
        console.log("REGISTER BODY:", req.body);

        const full_name = (req.body.full_name || req.body.fullName || "").trim();
        const username = (req.body.username || "").trim();
        const email = (req.body.email || "").trim().toLowerCase();
        const password = req.body.password || "";
        const confirm_password = req.body.confirm_password || req.body.confirmPassword || "";
        const phone_number = (req.body.phone_number || req.body.phone || "").trim();

        if (!full_name || !username || !email || !password || !confirm_password) {
            return res.status(400).json({
                success: false,
                message: "Vui lòng nhập đầy đủ họ tên, username, email và mật khẩu.",
                received: req.body
            });
        }

        if (password !== confirm_password) {
            return res.status(400).json({
                success: false,
                message: "Mật khẩu xác nhận không khớp."
            });
        }

        if (password.length < 6) {
            return res.status(400).json({
                success: false,
                message: "Mật khẩu phải có ít nhất 6 ký tự."
            });
        }

        const usersRef = db.collection("users");

        const existingEmail = await usersRef.where("email", "==", email).get();
        if (!existingEmail.empty) {
            return res.status(400).json({
                success: false,
                message: "Email đã tồn tại."
            });
        }

        const existingUsername = await usersRef.where("username", "==", username).get();
        if (!existingUsername.empty) {
            return res.status(400).json({
                success: false,
                message: "Tên đăng nhập đã tồn tại."
            });
        }

        if (phone_number !== "") {
            const existingPhone = await usersRef
                .where("phone_number", "==", phone_number)
                .get();

            if (!existingPhone.empty) {
                return res.status(400).json({
                    success: false,
                    message: "Số điện thoại đã tồn tại."
                });
            }
        }

        const userRef = await usersRef.add({
            full_name: full_name,
            username: username,
            email: email,
            password: password,
            phone_number: phone_number,
            roles: ["student"],
            avatar_url: "",
            auth_provider: "normal",
            created_at: admin.firestore.FieldValue.serverTimestamp(),
            updated_at: null
        });

        return res.json({
            success: true,
            message: "Đăng ký thành công.",
            user: {
                user_id: userRef.id,
                full_name: full_name,
                username: username,
                email: email,
                phone_number: phone_number,
                avatar_url: "",
                roles: ["student"]
            }
        });

    } catch (error) {
        console.error("Register error:", error);

        return res.status(500).json({
            success: false,
            message: "Lỗi server khi đăng ký.",
            error: error.message
        });
    }
});

// -----------------------------Login------------------------------
app.post("/api/login", async (req, res) => {
    try {
        const { account, password } = req.body;

        if (!account || !password) {
            return res.status(400).json({
                success: false,
                message: "Vui lòng nhập tên đăng nhập/email và mật khẩu."
            });
        }

        const usersRef = db.collection("users");

        let snapshot = await usersRef
            .where("email", "==", account)
            .where("password", "==", password)
            .get();

        if (snapshot.empty) {
            snapshot = await usersRef
                .where("username", "==", account)
                .where("password", "==", password)
                .get();
        }

        if (snapshot.empty) {
            return res.status(401).json({
                success: false,
                message: "Tên đăng nhập/email hoặc mật khẩu không đúng."
            });
        }

        const userDoc = snapshot.docs[0];
        const userData = userDoc.data();

        res.json({
            success: true,
            message: "Đăng nhập thành công.",
            user: {
                user_id: userDoc.id,
                full_name: userData.full_name,
                username: userData.username,
                email: userData.email,
                phone_number: userData.phone_number || "",
                avatar_url: userData.avatar_url || "",
                roles: userData.roles || ["student"]
            }
        });

    } catch (error) {
        console.error("Login error:", error);

        res.status(500).json({
            success: false,
            message: error.message
        });
    }
});

// ----------------------------- Google Login ------------------------------
app.post("/api/google-login", async (req, res) => {
    try {
        const firebase_uid = req.body.firebase_uid || req.body.uid || "";
        const email = req.body.email || "";
        const full_name = req.body.full_name || req.body.name || "";
        const avatar_url = req.body.avatar_url || req.body.user_img || req.body.photo_url || "";

        if (!firebase_uid || !email) {
            return res.status(400).json({
                success: false,
                message: "Thiếu firebase_uid hoặc email."
            });
        }

        const usersRef = db.collection("users");

        // 1. Nếu user đã tồn tại theo firebase_uid
        let snapshot = await usersRef
            .where("firebase_uid", "==", firebase_uid)
            .get();

        if (!snapshot.empty) {
            const userDoc = snapshot.docs[0];
            const userData = userDoc.data();

            return res.json({
                success: true,
                message: "Đăng nhập Google thành công.",
                user: {
                    user_id: userDoc.id,
                    firebase_uid: userData.firebase_uid || firebase_uid,
                    full_name: userData.full_name || "",
                    username: userData.username || "",
                    email: userData.email || "",
                    phone_number: userData.phone_number || "",
                    avatar_url: userData.avatar_url || "",
                    roles: userData.roles || ["student"]
                }
            });
        }

        // 2. Nếu chưa có firebase_uid, kiểm tra email đã có trong users chưa
        snapshot = await usersRef
            .where("email", "==", email)
            .get();

        if (!snapshot.empty) {
            const userDoc = snapshot.docs[0];
            const userData = userDoc.data();

            await usersRef.doc(userDoc.id).update({
                firebase_uid: firebase_uid,
                auth_provider: "google",
                avatar_url: userData.avatar_url || avatar_url || "",
                updated_at: new Date()
            });

            return res.json({
                success: true,
                message: "Liên kết tài khoản Google thành công.",
                user: {
                    user_id: userDoc.id,
                    firebase_uid: firebase_uid,
                    full_name: userData.full_name || full_name || "",
                    username: userData.username || "",
                    email: userData.email || email,
                    phone_number: userData.phone_number || "",
                    avatar_url: userData.avatar_url || avatar_url || "",
                    roles: userData.roles || ["student"]
                }
            });
        }

        // 3. Nếu hoàn toàn chưa có user thì tạo mới
        const usernameBase = email.split("@")[0];
        let username = usernameBase;

        let count = 1;
        while (true) {
            const usernameSnapshot = await usersRef
                .where("username", "==", username)
                .get();

            if (usernameSnapshot.empty) break;

            username = usernameBase + count;
            count++;
        }

        const newUserRef = await usersRef.add({
            firebase_uid: firebase_uid,
            full_name: full_name || usernameBase,
            username: username,
            email: email,
            password: "",
            phone_number: "",
            avatar_url: avatar_url || "",
            roles: ["student"],
            auth_provider: "google",
            created_at: new Date(),
            updated_at: null
        });

        return res.json({
            success: true,
            message: "Tạo user Google thành công.",
            user: {
                user_id: newUserRef.id,
                firebase_uid: firebase_uid,
                full_name: full_name || usernameBase,
                username: username,
                email: email,
                phone_number: "",
                avatar_url: avatar_url || "",
                roles: ["student"]
            }
        });

    } catch (error) {
        console.error("Google login error:", error);

        return res.status(500).json({
            success: false,
            message: error.message
        });
    }
});


// --------------------Take User Info (UserInfoScene) ---------------------------------
app.get("/api/users/:userId", async (req, res) => {
    try {
        const userId = req.params.userId;

        const userDoc = await db.collection("users").doc(userId).get();

        if (!userDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "User not found"
            });
        }

        const userData = userDoc.data();

        res.json({
            success: true,
            user: {
                user_id: userDoc.id,
                full_name: userData.full_name || "",
                username: userData.username || "",
                email: userData.email || "",
                password: userData.password || "",
                phone_number: userData.phone_number || "",
                avatar_url: userData.avatar_url || "",
                roles: userData.roles || ["student"]
            }
        });

    } catch (error) {
        console.error("Get user info error:", error);

        res.status(500).json({
            success: false,
            message: error.message
        });
    }
});
// -----------------------------------------------

// ----------------------Update User Info (UserInfoScene)-------------
app.put("/api/users/:userId", async (req, res) => {
    try {
        const userId = req.params.userId;
        const { field, value } = req.body;

        const allowedFields = [
            "full_name",
            "username",
            "email",
            "password",
            "phone_number"
        ];

        if (!field || value === undefined) {
            return res.status(400).json({
                success: false,
                message: "Thiếu field hoặc value."
            });
        }

        if (!allowedFields.includes(field)) {
            return res.status(400).json({
                success: false,
                message: "Field không hợp lệ."
            });
        }

        const userRef = db.collection("users").doc(userId);
        const userDoc = await userRef.get();

        if (!userDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "User not found."
            });
        }

        await userRef.update({
            [field]: value
        });

        res.json({
            success: true,
            message: "Cập nhật thành công."
        });

    } catch (error) {
        console.error("Update user error:", error);

        res.status(500).json({
            success: false,
            message: error.message
        });
    }
});
// --------------------------------------------------


// ---------------------- Upload Lesson PDF ----------------------

app.post("/api/upload/lesson-pdf", uploadPdf.single("pdf"), async (req, res) => {
    try {
        if (!req.file) {
            return res.status(400).json({
                success: false,
                message: "No PDF file uploaded."
            });
        }

        const result = await uploadFileToFirebase(req.file, "lesson_pdfs");

        res.json({
            success: true,
            message: "Upload lesson PDF thành công.",
            pdf_name: result.file_name,
            pdf_url: result.file_url
        });

    } catch (error) {
        console.error("Upload lesson PDF error:", error);

        res.status(500).json({
            success: false,
            message: error.message
        });
    }
});

// ===============================
// UPLOAD LESSON VIDEO TO FIREBASE
// ===============================
const uploadLessonVideo = multer({
    storage: multer.memoryStorage(),
    limits: { fileSize: 300 * 1024 * 1024 } // Giới hạn 300MB
});

app.post(
    "/api/upload/lesson-video",
    uploadLessonVideo.single("video_file"),
    async (req, res) => {
        try {
            if (!req.file) {
                return res.status(400).json({ success: false, message: "Không có file video" });
            }

            // Gọi hàm đã sửa lúc trước hỗ trợ .mp4
           const result = await convertAndUploadVideoToFirebase(req.file, "lesson_videos");

            res.json({
                success: true,
                message: "Upload video thành công",
                video_url: result.file_url,
                file_name: result.file_name
            });

        } catch (error) {
            console.error("Upload video error:", error);
            res.status(500).json({ success: false, message: error.message });
        }
    }
);
// ===============================
// GET CLASSES BY STUDENT - FIREBASE
// ===============================
app.get("/api/students/:studentId/classes", async (req, res) => {
    try {
        const studentId = req.params.studentId;

        const enrollmentSnapshot = await db
            .collection("class_enrollments")
            .where("student_id", "==", studentId)
            .get();

        const classes = [];

        for (const enrollDoc of enrollmentSnapshot.docs) {
            const enrollData = enrollDoc.data();
            const classId = enrollData.class_id;

            const classDoc = await db.collection("classes").doc(classId).get();

            if (!classDoc.exists) {
                continue;
            }

            const classData = classDoc.data();

            let teacherName = "";
            let teacherImg = "";

            if (classData.teacher_id) {
                const teacherDoc = await db
                    .collection("users")
                    .doc(classData.teacher_id)
                    .get();

                if (teacherDoc.exists) {
                    const teacherData = teacherDoc.data();
                    teacherName = teacherData.full_name || "";
                    teacherImg = teacherData.avatar_url || "";
                }
            }

            classes.push({
                class_id: classDoc.id,
                class_name: classData.class_name || "",
                summary_info: classData.summary_info || "",
                class_img: classData.class_img || "",
                class_img_url: classData.class_img_url || classData.class_img || "",
                teacher_id: classData.teacher_id || "",
                teacher_name: teacherName,
                teacher_img: teacherImg,
                created_at: classData.created_at || null,
                joined_at: enrollData.joined_at || null
            });
        }

        classes.sort((a, b) => {
            const timeA = a.joined_at && a.joined_at.toMillis
                ? a.joined_at.toMillis()
                : 0;

            const timeB = b.joined_at && b.joined_at.toMillis
                ? b.joined_at.toMillis()
                : 0;

            return timeB - timeA;
        });

        res.json({
            success: true,
            classes: classes
        });

    } catch (error) {
        console.error("Get student classes error:", error);

        res.status(500).json({
            success: false,
            message: "Database error",
            error: error.message
        });
    }
});

// ===============================
// DELETE CLASS FROM TEACHER - FIREBASE
// ===============================
app.delete("/api/classes/:classId", async (req, res) => {
    try {
        const classId = req.params.classId;

        const classRef = db.collection("classes").doc(classId);
        const classDoc = await classRef.get();

        if (!classDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy lớp học"
            });
        }

        const enrollmentSnapshot = await db
            .collection("class_enrollments")
            .where("class_id", "==", classId)
            .get();

        const removedStudents = [];

        for (const enrollDoc of enrollmentSnapshot.docs) {
            const enrollData = enrollDoc.data();
            const studentId = enrollData.student_id;

            let studentInfo = {
                student_id: studentId,
                full_name: "",
                email: ""
            };

            if (studentId) {
                const studentDoc = await db
                    .collection("users")
                    .doc(studentId)
                    .get();

                if (studentDoc.exists) {
                    const studentData = studentDoc.data();

                    studentInfo.full_name = studentData.full_name || "";
                    studentInfo.email = studentData.email || "";
                }
            }

            removedStudents.push(studentInfo);
        }

        const batch = db.batch();

        enrollmentSnapshot.docs.forEach((doc) => {
            batch.delete(doc.ref);
        });

        batch.delete(classRef);

        await batch.commit();

        res.json({
            success: true,
            message: "Xóa lớp học thành công",
            removed_students_count: removedStudents.length,
            removed_students: removedStudents
        });

    } catch (error) {
        console.error("Delete class error:", error);

        res.status(500).json({
            success: false,
            message: "Lỗi khi xóa lớp học",
            error: error.message
        });
    }
});

// ===============================
// DELETE LESSON - FIREBASE
// ===============================
app.delete("/api/lessons/:lessonId", async (req, res) => {
    try {
        const lessonId = req.params.lessonId;

        const lessonRef = db.collection("lessons").doc(lessonId);
        const lessonDoc = await lessonRef.get();

        if (!lessonDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy bài học"
            });
        }

        const lessonData = lessonDoc.data();
        const quizId = lessonData.quiz_id || "";

        const batch = db.batch();

        // Xóa quiz + questions nếu bài học có quiz
        if (quizId !== "") {
            const quizRef = db.collection("quizzes").doc(quizId);
            const questionsSnapshot = await quizRef.collection("questions").get();

            questionsSnapshot.forEach((doc) => {
                batch.delete(doc.ref);
            });

            batch.delete(quizRef);
        }

        // Xóa lesson
        batch.delete(lessonRef);

        await batch.commit();

        res.json({
            success: true,
            message: "Xóa bài học thành công",
            lesson_id: lessonId
        });

    } catch (error) {
        console.error("Delete lesson error:", error);

        res.status(500).json({
            success: false,
            message: "Lỗi khi xóa bài học",
            error: error.message
        });
    }
});

// ===============================
// UPLOAD CLASS IMAGE TO FIREBASE STORAGE
// ===============================
const image_class_upload = multer({
    storage: multer.memoryStorage()
});

app.post(
    "/api/upload/class-image",
    image_class_upload.single("class_img"),
    async (req, res) => {
        try {
            if (!req.file) {
                return res.status(400).json({
                    success: false,
                    message: "Không có file ảnh"
                });
            }

            const fileName =
                "class_images/" + Date.now() + "-" + req.file.originalname;

            const file = bucket.file(fileName);

            await file.save(req.file.buffer, {
                metadata: {
                    contentType: req.file.mimetype
                }
            });

            await file.makePublic();

            const imageUrl = `https://storage.googleapis.com/${bucket.name}/${fileName}`;

            res.json({
                success: true,
                image_url: imageUrl,
                file_name: fileName
            });

        } catch (error) {
            console.error("Upload class image error:", error);

            res.status(500).json({
                success: false,
                message: "Lỗi khi upload ảnh lớp học",
                error: error.message
            });
        }
    }
);

// ===============================
// CREATE CLASS - FIREBASE
// ===============================
app.post("/api/classes", async (req, res) => {
    try {
        const {
            class_name,
            summary_info,
            class_img,
            class_img_url,
            teacher_id
        } = req.body;

        if (!class_name || !summary_info || !teacher_id) {
            return res.status(400).json({
                success: false,
                message: "Thiếu thông tin lớp học"
            });
        }

        const teacherDoc = await db
            .collection("users")
            .doc(teacher_id)
            .get();

        if (!teacherDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy giáo viên"
            });
        }

        const finalImageUrl = class_img_url || class_img || "";

        const newClassRef = await db.collection("classes").add({
            class_name: class_name,
            summary_info: summary_info,
            class_img: finalImageUrl,
            class_img_url: finalImageUrl,
            teacher_id: teacher_id,
            created_at: admin.firestore.FieldValue.serverTimestamp()
        });

        res.json({
            success: true,
            message: "Tạo lớp học thành công",
            class_id: newClassRef.id,
            class_img_url: finalImageUrl
        });

    } catch (error) {
        console.error("Create class error:", error);

        res.status(500).json({
            success: false,
            message: "Lỗi khi tạo lớp học",
            error: error.message
        });
    }
});

app.get("/api/teachers/:teacherId/classes", async (req, res) => {
    try {
        const teacherId = req.params.teacherId;

        const teacherDoc = await db
            .collection("users")
            .doc(teacherId)
            .get();

        if (!teacherDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy giáo viên"
            });
        }

        const teacherData = teacherDoc.data();

        const snapshot = await db
            .collection("classes")
            .where("teacher_id", "==", teacherId)
            .get();

        const classes = [];

        snapshot.forEach((doc) => {
            const data = doc.data();

            classes.push({
                class_id: doc.id,
                class_name: data.class_name || "",
                summary_info: data.summary_info || "",
                class_img: data.class_img || "",
                class_img_url: data.class_img_url || data.class_img || "",
                teacher_id: data.teacher_id || "",
                teacher_name: teacherData.full_name || "",
                teacher_img: teacherData.avatar_url || "",
                created_at: data.created_at || null
            });
        });

        classes.sort((a, b) => {
            const timeA = a.created_at && a.created_at.toMillis
                ? a.created_at.toMillis()
                : 0;

            const timeB = b.created_at && b.created_at.toMillis
                ? b.created_at.toMillis()
                : 0;

            return timeB - timeA;
        });

        res.json({
            success: true,
            classes: classes
        });

    } catch (error) {
        console.error("Get teacher classes error:", error);

        res.status(500).json({
            success: false,
            message: "Database error",
            error: error.message
        });
    }
});

// ===============================
// UPDATE CLASS - FIREBASE
// ===============================
app.put("/api/classes/:classId", async (req, res) => {
    try {
        const classId = req.params.classId;

        const {
            class_name,
            summary_info,
            class_img,
            class_img_url
        } = req.body;

        if (!class_name || !summary_info) {
            return res.status(400).json({
                success: false,
                message: "Thiếu thông tin lớp học"
            });
        }

        const classRef = db.collection("classes").doc(classId);
        const classDoc = await classRef.get();

        if (!classDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy lớp học"
            });
        }

        const finalImageUrl = class_img_url || class_img || "";

        await classRef.update({
            class_name: class_name,
            summary_info: summary_info,
            class_img: finalImageUrl,
            class_img_url: finalImageUrl,
            updated_at: admin.firestore.FieldValue.serverTimestamp()
        });

        res.json({
            success: true,
            message: "Cập nhật lớp học thành công",
            class_id: classId
        });

    } catch (error) {
        console.error("Update class error:", error);

        res.status(500).json({
            success: false,
            message: "Lỗi khi cập nhật lớp học",
            error: error.message
        });
    }
});

// ===============================
// UPLOAD LESSON IMAGE TO FIREBASE STORAGE
// ===============================
const uploadLessonImage = multer({
    storage: multer.memoryStorage(),
    fileFilter: function (req, file, cb) {
        if (
            file.mimetype === "image/png" ||
            file.mimetype === "image/jpg" ||
            file.mimetype === "image/jpeg"
        ) {
            cb(null, true);
        } else {
            cb(new Error("Only image files are allowed"));
        }
    },
    limits: {
        fileSize: 50 * 1024 * 1024
    }
});

app.post(
    "/api/upload/lesson-image",
    uploadLessonImage.single("lesson_img"),
    async (req, res) => {
        try {
            if (!req.file) {
                return res.status(400).json({
                    success: false,
                    message: "Không có file ảnh"
                });
            }

            const safeName = req.file.originalname.replace(/\s+/g, "_");
            const fileName = "lesson_images/" + Date.now() + "-" + safeName;

            const file = bucket.file(fileName);

            await file.save(req.file.buffer, {
                metadata: {
                    contentType: req.file.mimetype
                }
            });

            await file.makePublic();

            const imageUrl = `https://storage.googleapis.com/${bucket.name}/${fileName}`;

            res.json({
                success: true,
                message: "Upload lesson image thành công",
                image_url: imageUrl,
                file_name: fileName
            });

        } catch (error) {
            console.error("Upload lesson image error:", error);

            res.status(500).json({
                success: false,
                message: "Lỗi khi upload ảnh bài học",
                error: error.message
            });
        }
    }
);

// ===============================
// CREATE LESSON - FIREBASE
// ===============================
const upload = multer({
    storage: multer.memoryStorage(),
    limits: {
        fileSize: 300 * 1024 * 1024
    }
});

// ===============================
// CREATE LESSON - FIREBASE
// ===============================
app.post("/api/lessons", upload.fields([
    { name: "lesson_img", maxCount: 1 },
    { name: "lesson_pdf", maxCount: 1 },
    { name: "exercise_pdf", maxCount: 1 },
    { name: "video_file", maxCount: 1 }
]), async (req, res) => {
    try {
        const {
            class_id,
            teacher_id,
            lesson_title,
            lesson_info,
            time_exercise,
            deadline_date,
            deadline_time,
            lesson_img_url: body_lesson_img_url,
            lesson_pdf_url: body_lesson_pdf_url,
            exercise_pdf_url: body_exercise_pdf_url,
            models,
            video_url: body_video_url
        } = req.body;

        if (!class_id || !teacher_id || !lesson_title || !lesson_info) {
            return res.status(400).json({
                success: false,
                message: "Thiếu thông tin bài học"
            });
        }

        let lesson_img_url = body_lesson_img_url || "";
        let lesson_pdf_url = body_lesson_pdf_url || "";
        let exercise_pdf_url = body_exercise_pdf_url || "";
        let exercisePdfStorageName = "";
        let video_url = body_video_url || "";
        // Upload lesson image
        if (req.files && req.files.lesson_img && req.files.lesson_img.length > 0) {
            const uploadResult = await uploadFileToFirebase(
                req.files.lesson_img[0],
                "lesson_images"
            );

            lesson_img_url = uploadResult.file_url;
        }

        // Upload lesson PDF
        if (req.files && req.files.lesson_pdf && req.files.lesson_pdf.length > 0) {
            const uploadResult = await uploadFileToFirebase(
                req.files.lesson_pdf[0],
                "lesson_pdfs"
            );

            lesson_pdf_url = uploadResult.file_url;
        }
        if (req.files && req.files.video_file && req.files.video_file.length > 0) {
            // ĐÃ THAY BẰNG HÀM MỚI
            const uploadResult = await convertAndUploadVideoToFirebase(
                req.files.video_file[0],
                "lesson_videos"
            );
            video_url = uploadResult.file_url;
        }
        const lessonRef = db.collection("lessons").doc();
        const lessonId = lessonRef.id;

        let parsedModels = [];
        if (models) {
            try {
                parsedModels = typeof models === "string" ? JSON.parse(models) : models;
            } catch (e) {
                console.warn("Không parse được mảng models:", e);
            }
        }

        // Create lesson first
        await lessonRef.set({
            lesson_id: lessonId,
            class_id: class_id,
            teacher_id: teacher_id,

            lesson_title: lesson_title,
            lesson_info: lesson_info,

            lesson_img: lesson_img_url,
            lesson_img_url: lesson_img_url,

            lesson_pdf_url: lesson_pdf_url,
            exercise_pdf_url: exercise_pdf_url,

            quiz_id: "",

            time_exercise: time_exercise || "",
            deadline_date: deadline_date || "",
            deadline_time: deadline_time || "",
            models: parsedModels,
            video_url: video_url,
            created_at: admin.firestore.FieldValue.serverTimestamp(),
            updated_at: null
        });

        let quizId = "";

        const hasExercisePdfFile =
            req.files &&
            req.files.exercise_pdf &&
            req.files.exercise_pdf.length > 0;

        const hasExercisePdfUrl =
            exercise_pdf_url &&
            exercise_pdf_url.trim() !== "";

        // If lesson has quiz PDF
        if (hasExercisePdfFile || hasExercisePdfUrl) {
            let quizPdfName = "";
            let quizPdfUrl = exercise_pdf_url;

            if (hasExercisePdfFile) {
                const exercisePdfFile = req.files.exercise_pdf[0];

                const uploadResult = await uploadFileToFirebase(
                    exercisePdfFile,
                    "quiz_pdfs"
                );

                quizPdfName = uploadResult.file_name;
                quizPdfUrl = uploadResult.file_url;
                exercise_pdf_url = uploadResult.file_url;
                exercisePdfStorageName = uploadResult.file_name;
            }

            const quizRef = db.collection("quizzes").doc();
            quizId = quizRef.id;

            await quizRef.set({
                quiz_id: quizId,
                lesson_id: lessonId,
                class_id: class_id,
                teacher_id: teacher_id,

                quiz_title: lesson_title,
                quiz_pdf_name: quizPdfName,
                quiz_pdf_url: quizPdfUrl,

                time_exercise: time_exercise || "",
                deadline_date: deadline_date || "",
                deadline_time: deadline_time || "",

                extract_status: "processing",
                extract_message: "",
                total_questions: 0,

                created_at: admin.firestore.FieldValue.serverTimestamp(),
                updated_at: null
            });

            await lessonRef.update({
                quiz_id: quizId,
                exercise_pdf_url: quizPdfUrl,
                updated_at: admin.firestore.FieldValue.serverTimestamp()
            });

            if (hasExercisePdfFile && exercisePdfStorageName !== "") {
                try {
                    const tempDir = path.join(__dirname, "temp", "quiz_pdfs");

                    if (!fs.existsSync(tempDir)) {
                        fs.mkdirSync(tempDir, { recursive: true });
                    }

                    const tempPdfPath = path.join(tempDir, quizId + ".pdf");

                    await downloadPdfFromFirebaseStorage(
                        exercisePdfStorageName,
                        tempPdfPath
                    );

                    const pageImages = await convertPdfToImages(tempPdfPath, quizId);
                    const regions = await extractQuestionRegionsFromPdf(tempPdfPath, quizId);

                    if (regions.length === 0) {
                        await quizRef.update({
                            extract_status: "failed",
                            extract_message: "No question regions found in PDF",
                            total_questions: 0
                        });
                    } else {
                        const croppedQuestions = await cropQuestionsFromRegions(
                            pageImages,
                            regions,
                            quizId
                        );

                        const batch = db.batch();

                        for (let i = 0; i < croppedQuestions.length; i++) {
                            const q = croppedQuestions[i];

                            const questionRef = quizRef
                                .collection("questions")
                                .doc(String(q.question_order));

                            batch.set(questionRef, {
                                question_id: questionRef.id,
                                quiz_id: quizId,
                                lesson_id: lessonId,
                                question_order: q.question_order,
                                question_img_name: q.question_img_name || "",
                                question_img_url: q.question_img_url || "",
                                correct_answer: q.correct_answer || "",
                                created_at: admin.firestore.FieldValue.serverTimestamp()
                            });
                        }

                        batch.update(quizRef, {
                            extract_status: "done",
                            extract_message: "",
                            total_questions: croppedQuestions.length,
                            extracted_at: admin.firestore.FieldValue.serverTimestamp()
                        });

                        await batch.commit();
                    }
                } catch (extractError) {
                    console.error("Extract quiz questions error:", extractError);

                    await quizRef.update({
                        extract_status: "failed",
                        extract_message: extractError.message,
                        total_questions: 0
                    });
                }
            } else {
                await quizRef.update({
                    extract_status: "waiting_file",
                    extract_message: "Quiz PDF URL exists, but no exercise_pdf file was uploaded. Cannot crop questions from URL safely.",
                    total_questions: 0
                });
            }
        }

        return res.json({
            success: true,
            message: "Tạo bài học thành công",
            lesson_id: lessonId,
            quiz_id: quizId
        });

    } catch (error) {
        console.error("Create lesson error:", error);

        return res.status(500).json({
            success: false,
            message: "Lỗi server khi tạo bài học",
            error: error.message
        });
    }
});

// ===============================
// UPDATE LESSON - FIREBASE
// ===============================
app.put("/api/lessons/:lessonId", upload.fields([
    { name: "lesson_img", maxCount: 1 },
    { name: "lesson_pdf", maxCount: 1 },
    { name: "exercise_pdf", maxCount: 1 },
    { name: "video_file", maxCount: 1 } 
]), async (req, res) => {
    try {
        const lessonId = req.params.lessonId;

        const {
            class_id,
            teacher_id,
            lesson_title,
            lesson_info,
            time_exercise,
            deadline_date,
            deadline_time,
            lesson_img,
            lesson_img_url,
            lesson_pdf_url,
            exercise_pdf_url,
            models,
            video_url: body_video_url
        } = req.body;

        if (!lesson_title || !lesson_info) {
            return res.status(400).json({
                success: false,
                message: "Thiếu thông tin bài học"
            });
        }

        const lessonRef = db.collection("lessons").doc(lessonId);
        const lessonDoc = await lessonRef.get();

        if (!lessonDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy bài học"
            });
        }

        const oldLessonData = lessonDoc.data();

        const finalLessonImgUrl =
            lesson_img_url ||
            lesson_img ||
            oldLessonData.lesson_img_url ||
            oldLessonData.lesson_img ||
            "";

        const finalLessonPdfUrl =
            lesson_pdf_url ||
            oldLessonData.lesson_pdf_url ||
            "";

        const finalExercisePdfUrl =
            exercise_pdf_url ||
            oldLessonData.exercise_pdf_url ||
            "";
        let finalVideoUrl = body_video_url || oldLessonData.video_url || "";
        
        if (req.files && req.files.video_file && req.files.video_file.length > 0) {
            const uploadResult = await convertAndUploadVideoToFirebase(
                req.files.video_file[0],
                "lesson_videos"
            );
            finalVideoUrl = uploadResult.file_url;
        }
        let parsedModels = [];
        if (models) {
            try {
                parsedModels = typeof models === "string" ? JSON.parse(models) : models;
            } catch (e) {
                console.warn("Không parse được mảng models:", e);
                // Giữ lại models cũ nếu lỗi
                parsedModels = oldLessonData.models || []; 
            }
        } else {
            // Nếu Unity không gửi lên, giữ nguyên danh sách model cũ
            parsedModels = oldLessonData.models || [];
        }
        await lessonRef.update({
            class_id: class_id || oldLessonData.class_id || "",
            teacher_id: teacher_id || oldLessonData.teacher_id || "",

            lesson_title: lesson_title,
            lesson_info: lesson_info,

            time_exercise: time_exercise || oldLessonData.time_exercise || "",
            deadline_date: deadline_date || oldLessonData.deadline_date || "",
            deadline_time: deadline_time || oldLessonData.deadline_time || "",

            lesson_img: finalLessonImgUrl,
            lesson_img_url: finalLessonImgUrl,

            lesson_pdf_url: finalLessonPdfUrl,
            exercise_pdf_url: finalExercisePdfUrl,

            models: parsedModels,
            video_url: finalVideoUrl,

            updated_at: admin.firestore.FieldValue.serverTimestamp()
        });

        res.json({
            success: true,
            message: "Cập nhật bài học thành công",
            lesson_id: lessonId
        });

    } catch (error) {
        console.error("Update lesson error:", error);

        res.status(500).json({
            success: false,
            message: "Lỗi khi cập nhật bài học",
            error: error.message
        });
    }
});

// ---------- GET LESSON IN CLASS ----------
app.get("/api/lessons/class/:classId", async (req, res) => {
    try {
        const classId = req.params.classId;

        const snapshot = await db
            .collection("lessons")
            .where("class_id", "==", classId)
            .get();

        const lessons = [];

        for (const doc of snapshot.docs) {
            const data = doc.data();

            let teacherName = "";
            let teacherImg = "";

            if (data.teacher_id) {
                const teacherDoc = await db.collection("users").doc(data.teacher_id).get();

                if (teacherDoc.exists) {
                    const teacherData = teacherDoc.data();
                    teacherName = teacherData.full_name || "";
                    teacherImg = teacherData.avatar_url || "";
                }
            }

            lessons.push({
                lesson_id: doc.id,
                class_id: data.class_id || "",
                lesson_title: data.lesson_title || "",
                lesson_info: data.lesson_info || "",
                lesson_img: data.lesson_img || "",
                lesson_img_url: data.lesson_img_url || data.lesson_img || "",
                lesson_pdf_url: data.lesson_pdf_url || "",
                exercise_pdf_url: data.exercise_pdf_url || "",
                quiz_id: data.quiz_id || "",
                teacher_id: data.teacher_id || "",
                teacher_name: teacherName,
                teacher_img: teacherImg,
                models: data.models || [],
                time_exercise: data.time_exercise || "",
                deadline_date: data.deadline_date || "",
                deadline_time: data.deadline_time || "",
                created_at: data.created_at || null
            });
        }

        lessons.sort((a, b) => {
            const timeA = a.created_at && a.created_at.toMillis
                ? a.created_at.toMillis()
                : 0;

            const timeB = b.created_at && b.created_at.toMillis
                ? b.created_at.toMillis()
                : 0;

            return timeB - timeA;
        });

        res.json({
            success: true,
            lessons: lessons
        });

    } catch (error) {
        console.error("Get lessons by class error:", error);

        res.status(500).json({
            success: false,
            message: "Database error",
            error: error.message
        });
    }
});

// ===============================
// GET LESSON DETAIL - FIREBASE
// ===============================
app.get("/api/lessons/:lessonId", async (req, res) => {
    try {
        const lessonId = req.params.lessonId;

        const lessonDoc = await db.collection("lessons").doc(lessonId).get();

        if (!lessonDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy bài học"
            });
        }

        const data = lessonDoc.data();

        res.json({
            success: true,
            lesson: {
                lesson_id: lessonDoc.id,
                class_id: data.class_id || "",
                teacher_id: data.teacher_id || "",
                lesson_title: data.lesson_title || "",
                lesson_info: data.lesson_info || "",
                lesson_img: data.lesson_img || "",
                lesson_img_url: data.lesson_img_url || data.lesson_img || "",
                lesson_pdf_url: data.lesson_pdf_url || "",
                exercise_pdf_url: data.exercise_pdf_url || "",
                quiz_id: data.quiz_id || "",
                models: data.models || [],
                video_url: data.video_url || "",
                time_exercise: data.time_exercise || "",
                deadline_date: data.deadline_date || "",
                deadline_time: data.deadline_time || "",
                created_at: data.created_at || null,
                updated_at: data.updated_at || null
            }
        });

    } catch (error) {
        console.error("Get lesson detail error:", error);

        res.status(500).json({
            success: false,
            message: "Database error",
            error: error.message
        });
    }
});

// ===============================
// GET AVAILABLE CLASSES FOR STUDENT
// ===============================
app.get("/api/students/:studentId/available-classes", async (req, res) => {
    try {
        const studentId = req.params.studentId;

        const enrollSnapshot = await db
            .collection("class_enrollments")
            .where("student_id", "==", studentId)
            .get();

        const enrolledClassIds = new Set();

        enrollSnapshot.forEach(doc => {
            const data = doc.data();
            if (data.class_id) {
                enrolledClassIds.add(data.class_id);
            }
        });

        const classSnapshot = await db.collection("classes").get();

        const classes = [];

        for (const classDoc of classSnapshot.docs) {
            const classData = classDoc.data();

            if (classData.teacher_id === studentId) {
                continue;
            }

            if (enrolledClassIds.has(classDoc.id)) {
                continue;
            }

            let teacherName = "";
            let teacherImg = "";

            if (classData.teacher_id) {
                const teacherDoc = await db
                    .collection("users")
                    .doc(classData.teacher_id)
                    .get();

                if (teacherDoc.exists) {
                    const teacherData = teacherDoc.data();
                    teacherName = teacherData.full_name || "";
                    teacherImg = teacherData.avatar_url || "";
                }
            }

            classes.push({
                class_id: classDoc.id,
                class_name: classData.class_name || "",
                summary_info: classData.summary_info || "",
                class_img: classData.class_img || "",
                class_img_url: classData.class_img_url || classData.class_img || "",
                teacher_id: classData.teacher_id || "",
                teacher_name: teacherName,
                teacher_img: teacherImg,
                created_at: classData.created_at || null
            });
        }

        res.json({
            success: true,
            classes: classes
        });

    } catch (error) {
        console.error("Get available classes error:", error);

        res.status(500).json({
            success: false,
            message: "Database error",
            error: error.message
        });
    }
});

// ===============================
// REGISTER CLASS FOR STUDENT
// ===============================
// app.post("/api/students/:studentId/classes/:classId", async (req, res) => {
//     try {
//         const studentId = req.params.studentId;
//         const classId = req.params.classId;

//         const studentDoc = await db.collection("users").doc(studentId).get();

//         if (!studentDoc.exists) {
//             return res.status(404).json({
//                 success: false,
//                 message: "Không tìm thấy học sinh"
//             });
//         }

//         const classDoc = await db.collection("classes").doc(classId).get();

//         if (!classDoc.exists) {
//             return res.status(404).json({
//                 success: false,
//                 message: "Không tìm thấy lớp học"
//             });
//         }

//         const classData = classDoc.data();

//         if (classData.teacher_id === studentId) {
//             return res.status(400).json({
//                 success: false,
//                 message: "Bạn không thể đăng kí lớp do chính mình tạo"
//             });
//         }

//         const existing = await db
//             .collection("class_enrollments")
//             .where("student_id", "==", studentId)
//             .where("class_id", "==", classId)
//             .get();

//         if (!existing.empty) {
//             return res.status(400).json({
//                 success: false,
//                 message: "Bạn đã đăng kí lớp này rồi"
//             });
//         }

//         const enrollmentRef = await db.collection("class_enrollments").add({
//             student_id: studentId,
//             class_id: classId,
//             joined_at: admin.firestore.FieldValue.serverTimestamp()
//         });

//         res.json({
//             success: true,
//             message: "Đăng kí lớp học thành công",
//             enrollment_id: enrollmentRef.id
//         });

//     } catch (error) {
//         console.error("Register class error:", error);

//         res.status(500).json({
//             success: false,
//             message: "Lỗi khi đăng kí lớp học",
//             error: error.message
//         });
//     }
// });

app.post("/api/students/:studentId/classes/:classId", async (req, res) => {
    try {
        const studentId = req.params.studentId;
        const classId = req.params.classId;

        const existing = await db
            .collection("class_enrollments")
            .where("student_id", "==", studentId)
            .where("class_id", "==", classId)
            .get();

        if (!existing.empty) {
            return res.status(400).json({
                success: false,
                message: "Bạn đã đăng kí lớp này rồi"
            });
        }

        const enrollmentRef = await db.collection("class_enrollments").add({
            student_id: studentId,
            class_id: classId,
            joined_at: admin.firestore.FieldValue.serverTimestamp()
        });

        return res.json({
            success: true,
            message: "Đăng kí lớp học thành công",
            enrollment_id: enrollmentRef.id
        });

    } catch (error) {
        console.error("Register class error:", error);

        return res.status(500).json({
            success: false,
            message: "Lỗi khi đăng kí lớp học",
            error: error.message
        });
    }
});

// ------------------Delete Class from Student
app.delete("/api/students/:studentId/classes/:classId", async (req, res) => {
    try {
        const { studentId, classId } = req.params;

        const snapshot = await db
            .collection("class_enrollments")
            .where("student_id", "==", studentId)
            .where("class_id", "==", classId)
            .get();

        if (snapshot.empty) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy đăng kí lớp học"
            });
        }

        const batch = db.batch();

        snapshot.docs.forEach(doc => {
            batch.delete(doc.ref);
        });

        await batch.commit();

        res.json({
            success: true,
            message: "Hủy đăng kí lớp học thành công"
        });

    } catch (error) {
        res.status(500).json({
            success: false,
            message: "Lỗi khi hủy đăng kí lớp học",
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
    uploadQuizPdf.single("quiz_pdf"),
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
                models: data.models || [],
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
app.get("/api/quizzes/:quizId/questions", async (req, res) => {
    try {
        const { quizId } = req.params;

        const snapshot = await db
            .collection("quizzes")
            .doc(quizId)
            .collection("questions")
            .get();

        if (snapshot.empty) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy questions."
            });
        }

        const questions = [];

        snapshot.forEach((doc) => {
            const data = doc.data();

            questions.push({
                question_id: data.question_id || doc.id,
                question_order: Number(data.question_order || 0),
                question_image_url: data.question_image_url || data.question_img_url || "",
                correct_answer: data.correct_answer || ""
            });
        });

        questions.sort((a, b) => a.question_order - b.question_order);

        return res.json({
            success: true,
            quiz_id: quizId,
            total_questions: questions.length,
            questions: questions
        });

    } catch (error) {
        return res.status(500).json({
            success: false,
            message: "Lỗi khi lấy danh sách câu hỏi.",
            error: error.message
        });
    }
});

// // ===============================
// // GET QUIZ QUESTIONS BY QUIZ ID
// // Trả về total_questions + danh sách questions
// // ===============================
// app.get("/api/quizzes/:quizId/questions", async (req, res) => {
//     try {
//         const { quizId } = req.params;

//         if (!quizId) {
//             return res.status(400).json({
//                 success: false,
//                 message: "Thiếu quizId."
//             });
//         }

//         const quizRef = db.collection("quizzes").doc(quizId);

//         // KHÔNG check quizDoc.exists nữa
//         const snapshot = await quizRef
//             .collection("questions")
//             .get();

//         if (snapshot.empty) {
//             return res.status(404).json({
//                 success: false,
//                 message: "Không tìm thấy questions."
//             });
//         }

//         const questions = [];

//         snapshot.forEach((doc) => {
//             const data = doc.data();

//             questions.push({
//                 question_id: data.question_id || doc.id,
//                 question_order: Number(data.question_order || 0),
//                 question_image_url:
//                     data.question_image_url ||
//                     data.question_img_url ||
//                     "",
//                 correct_answer: data.correct_answer || ""
//             });
//         });

//         questions.sort((a, b) => a.question_order - b.question_order);

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
// GET STUDENT ATTEMPTS BY LESSON
// ===============================
app.get("/api/students/:studentId/lessons/:lessonId/attempts", async (req, res) => {
    try {
        const { studentId, lessonId } = req.params;

        const snapshot = await db
            .collection("attempts")
            .where("student_id", "==", studentId)
            .where("lesson_id", "==", lessonId)
            .get();

        const attempts = [];

        snapshot.forEach((doc) => {
            const data = doc.data();

            attempts.push({
                attempt_id: doc.id,
                student_id: data.student_id || "",
                lesson_id: data.lesson_id || "",
                quiz_id: data.quiz_id || "",
                score: data.score || "0/0",
                correct_count: data.correct_count || 0,
                total_questions: data.total_questions || 0,
                duration_seconds: data.duration_seconds || 0,
                status: data.status || "submitted",
                submitted_at: data.submitted_at && data.submitted_at.toDate
                    ? data.submitted_at.toDate().toISOString()
                    : "",
                attempt_number: 0
            });
        });

        attempts.sort((a, b) => {
            return new Date(a.submitted_at) - new Date(b.submitted_at);
        });

        for (let i = 0; i < attempts.length; i++) {
            attempts[i].attempt_number = i + 1;
        }

        res.json({
            success: true,
            attempts: attempts
        });
    } catch (error) {
        console.error("Get attempts error:", error);

        res.status(500).json({
            success: false,
            message: "Không lấy được attempts.",
            error: error.message
        });
    }
});

// ===============================
// GET STUDENT ATTEMPT STATUS
// ===============================
app.get("/api/students/:studentId/lessons/:lessonId/attempt-status", async (req, res) => {
    try {
        const { studentId, lessonId } = req.params;

        const snapshot = await db
            .collection("quiz_attempts")
            .where("student_id", "==", studentId)
            .where("lesson_id", "==", lessonId)
            .get();

        if (snapshot.empty) {
            return res.json({
                success: true,
                has_attempted: false,
                latest_attempt: null
            });
        }

        const attempts = [];

        snapshot.forEach((doc) => {
            const data = doc.data();

            attempts.push({
                attempt_id: doc.id,
                student_id: data.student_id || "",
                lesson_id: data.lesson_id || "",
                quiz_id: data.quiz_id || "",
                score: data.score || 0,
                total_questions: data.total_questions || 0,
                correct_count: data.correct_count || 0,
                submitted_at: data.submitted_at || null
            });
        });

        attempts.sort((a, b) => {
            const timeA = a.submitted_at && a.submitted_at.toMillis
                ? a.submitted_at.toMillis()
                : 0;

            const timeB = b.submitted_at && b.submitted_at.toMillis
                ? b.submitted_at.toMillis()
                : 0;

            return timeB - timeA;
        });

        return res.json({
            success: true,
            has_attempted: true,
            latest_attempt: attempts[0],
            attempts: attempts
        });

    } catch (error) {
        console.error("Get attempt status error:", error);

        return res.status(500).json({
            success: false,
            message: "Lỗi khi kiểm tra trạng thái làm bài.",
            error: error.message
        });
    }
});

app.post("/api/students/:studentId/lessons/:lessonId/attempts", async (req, res) => {
    try {
        const { quiz_id, score, correct_count, total_questions, duration_seconds, status } = req.body;

        const attemptRef = await db.collection("attempts").add({
            student_id: req.params.studentId,
            lesson_id: req.params.lessonId,
            quiz_id,
            score,
            correct_count,
            total_questions,
            duration_seconds,
            status: status || "submitted",
            submitted_at: new Date()
        });

        res.json({
            success: true,
            attempt_id: attemptRef.id
        });
    } catch (error) {
        res.status(500).json({
            success: false,
            message: error.message
        });
    }
});

app.get("/api/pdf-proxy", async (req, res) => {
    try {
        let pdfUrl = req.query.url || "";
        pdfUrl = decodeURIComponent(pdfUrl).trim();

        if (!pdfUrl) {
            return res.status(400).send("Missing PDF url");
        }

        if (pdfUrl.endsWith("?=")) {
            pdfUrl = pdfUrl.substring(0, pdfUrl.length - 2);
        }

        console.log("PDF proxy url:", pdfUrl);

        let filePath = "";

        const marker = bucket.name + "/";
        const index = pdfUrl.indexOf(marker);

        if (index >= 0) {
            filePath = pdfUrl.substring(index + marker.length);
            filePath = decodeURIComponent(filePath);
        }

        if (!filePath) {
            return res.status(400).send("Cannot extract Firebase Storage path");
        }

        console.log("PDF proxy filePath:", filePath);

        const file = bucket.file(filePath);
        const [exists] = await file.exists();

        if (!exists) {
            return res.status(404).send("PDF file does not exist in Firebase Storage: " + filePath);
        }

        const [buffer] = await file.download();

        res.setHeader("Content-Type", "application/pdf");
        res.setHeader("Content-Disposition", "inline; filename=lesson.pdf");
        return res.send(buffer);

    } catch (error) {
        console.error("PDF proxy error:", error);
        return res.status(500).send(error.message);
    }
});

app.get("/api/classes/:classId/members", async (req, res) => {
    try {
        const classId = req.params.classId;

        const classDoc = await db.collection("classes").doc(classId).get();

        if (!classDoc.exists) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy lớp học"
            });
        }

        const classData = classDoc.data();
        const members = [];

        // Lấy giáo viên
        if (classData.teacher_id) {
            const teacherDoc = await db.collection("users").doc(classData.teacher_id).get();

            if (teacherDoc.exists) {
                const teacherData = teacherDoc.data();

                members.push({
                    user_id: classData.teacher_id,
                    full_name: teacherData.full_name || "",
                    role: "teacher",
                    avatar_url: teacherData.avatar_url || ""
                });
            }
        }

        // Lấy học sinh từ class_enrollments
        const enrollmentSnapshot = await db
            .collection("class_enrollments")
            .where("class_id", "==", classId)
            .get();

        for (const enrollDoc of enrollmentSnapshot.docs) {
            const enrollData = enrollDoc.data();
            const studentId = enrollData.student_id;

            if (!studentId) continue;

            const studentDoc = await db.collection("users").doc(studentId).get();

            if (studentDoc.exists) {
                const studentData = studentDoc.data();

                members.push({
                    user_id: studentId,
                    full_name: studentData.full_name || "",
                    role: "student",
                    avatar_url: studentData.avatar_url || "",
                    joined_at: enrollData.joined_at || null
                });
            }
        }

        return res.json({
            success: true,
            members: members
        });

    } catch (error) {
        console.error("Get class members error:", error);

        return res.status(500).json({
            success: false,
            message: "Lỗi khi lấy danh sách thành viên",
            error: error.message
        });
    }
});


// app.listen(4000, () => {
//     console.log("Firebase server running on port 4000");
// });

app.listen(4000, "0.0.0.0", () => {
    console.log("Firebase server running on port 4000");
});