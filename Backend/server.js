const express = require("express");
const mysql = require("mysql2");
const cors = require("cors");
require("dotenv").config();

const app = express();
const multer = require("multer");
const { PDFParse } = require("pdf-parse");
const fs = require("fs");
const path = require("path");
const pdfPoppler = require("pdf-poppler");
const sharp = require("sharp");
const { execFile } = require("child_process");

app.use(cors());
app.use(express.json());

const classImageDir = path.join(__dirname, "uploads", "class_images");

if (!fs.existsSync(classImageDir)) {
    fs.mkdirSync(classImageDir, { recursive: true });
}
app.use("/uploads", express.static(path.join(__dirname, "uploads"))); // Upload

const db = mysql.createConnection({
    host: process.env.DB_HOST,
    user: process.env.DB_USER,
    password: process.env.DB_PASSWORD,
    database: process.env.DB_NAME
});

//------------------- Cấu hình upload quiz PDF ----------------------

const quizPdfDir = path.join(__dirname, "uploads", "quiz_pdfs");
const quizQuestionImgDir = path.join(__dirname, "uploads", "quiz_questions");

if (!fs.existsSync(quizPdfDir)) {
    fs.mkdirSync(quizPdfDir, { recursive: true });
}

if (!fs.existsSync(quizQuestionImgDir)) {
    fs.mkdirSync(quizQuestionImgDir, { recursive: true });
}

const quizStorage = multer.diskStorage({
    destination: function (req, file, cb) {
        cb(null, quizPdfDir);
    },
    filename: function (req, file, cb) {
        const safeName = file.originalname.replace(/\s+/g, "_");
        const uniqueName = Date.now() + "-" + safeName;
        cb(null, uniqueName);
    }
});

const uploadQuizPdf = multer({
    storage: quizStorage,
    fileFilter: function (req, file, cb) {
        if (file.mimetype === "application/pdf") {
            cb(null, true);
        } else {
            cb(new Error("Only PDF files are allowed"));
        }
    }
});

//---------------------------------------------------------

// ----------------------Cấu hình Upload Lesson PDF------------------
const lessonPdfDir = path.join(__dirname, "uploads", "lesson_pdfs");

if (!fs.existsSync(lessonPdfDir)) {
    fs.mkdirSync(lessonPdfDir, { recursive: true });
}

const lessonStorage = multer.diskStorage({
    destination: function (req, file, cb) {
        cb(null, lessonPdfDir);
    },
    filename: function (req, file, cb) {
        const safeName = file.originalname.replace(/\s+/g, "_");
        const uniqueName = Date.now() + "-" + safeName;
        cb(null, uniqueName);
    }
});

const lessonPdfUpload = multer({
    storage: lessonStorage,
    fileFilter: function (req, file, cb) {
        if (file.mimetype === "application/pdf") {
            cb(null, true);
        } else {
            cb(new Error("Only PDF files are allowed"));
        }
    }
});

// ----------------------------------------------------------------------------

db.connect((err) => {
    if (err) {
        console.log("Database connection failed:", err);
        return;
    }

    console.log("Connected to MySQL database");
});

app.get("/", (req, res) => {
    res.send("Virtual Education Backend is running");
});

app.get("/classes", (req, res) => {
    const sql = "SELECT * FROM classes";

    db.query(sql, (err, result) => {
        if (err) {
            return res.status(500).json({
                message: "Cannot get classes",
                error: err
            });
        }

        res.json(result);
    });
});



app.post("/register", (req, res) => {
    const { full_name, username, email, password, phone_number } = req.body;

    if (!full_name || !username || !email || !password || !phone_number) {
        return res.status(400).json({
            success: false,
            message: "Missing fields"
        });
    }

    const sql = `
        INSERT INTO users 
        (full_name, username, email, password, phone_number)
        VALUES (?, ?, ?, ?, ?)
    `;

    db.query(
        sql,
        [full_name, username, email, password, phone_number],
        (err, result) => {
            if (err) {
                console.log(err);

                return res.status(500).json({
                    success: false,
                    message: "Register failed",
                    error: err
                });
            }

            res.json({
                success: true,
                message: "Register successful",
                user_id: result.insertId
            });
        }
    );
});

app.post("/login", (req, res) => {
    const { usernameOrEmail, password } = req.body;

    if (!usernameOrEmail || !password) {
        return res.status(400).json({
            message: "Please fill in all fields."
        });
    }

    const findUserSql = `
        SELECT * FROM users
        WHERE username = ? OR email = ?
        LIMIT 1
    `;

    db.query(findUserSql, [usernameOrEmail, usernameOrEmail], (err, results) => {
        if (err) {
            return res.status(500).json({
                message: "Database error."
            });
        }

        if (results.length === 0) {
            return res.status(401).json({
                message: "Username or email is wrong."
            });
        }

        const user = results[0];

        if (user.password !== password) {
            return res.status(401).json({
                message: "Password is wrong."
            });
        }

        res.json({
            message: "Login successful",
            user_id: user.user_id,
            username: user.username,
            email: user.email,
            full_name: user.full_name
        });
    });
});

app.get("/api/users/:userId", (req, res) => {
    const userId = req.params.userId;

    const sql = `
        SELECT 
            user_id,
            full_name,
            username,
            email,
            password,
            user_img,
            phone_number
        FROM users
        WHERE user_id = ?
    `;

    db.query(sql, [userId], (err, result) => {

        if (err) {
            console.log(err);

            return res.status(500).json({
                success: false,
                message: "Database error"
            });
        }

        if (result.length === 0) {
            return res.status(404).json({
                success: false,
                message: "User not found"
            });
        }

        res.json({
            success: true,
            user: result[0]
        });
    });
});

app.post("/google-login", (req, res) => {
    const { full_name, email, user_img } = req.body;

    if (!email) {
        return res.status(400).json({
            success: false,
            message: "Missing Google email"
        });
    }

    const findUserSql = `
        SELECT * FROM users
        WHERE email = ?
        LIMIT 1
    `;

    db.query(findUserSql, [email], (err, results) => {
        if (err) {
            return res.status(500).json({
                success: false,
                message: "Database error"
            });
        }

        if (results.length > 0) {
            const user = results[0];

            return res.json({
                success: true,
                message: "Google login successful",
                user_id: user.user_id,
                username: user.username,
                email: user.email,
                full_name: user.full_name,
                user_img: user.user_img
            });
        }

        const username = email.split("@")[0];

        const insertSql = `
            INSERT INTO users
            (full_name, username, email, password, user_img, phone_number, login_type)
            VALUES (?, ?, ?, NULL, ?, '', 'google')
        `;

        db.query(
            insertSql,
            [full_name, username, email, user_img],
            (insertErr, result) => {
                if (insertErr) {
                    console.log(insertErr);

                    return res.status(500).json({
                        success: false,
                        message: "Cannot create Google user"
                    });
                }

                res.json({
                    success: true,
                    message: "Google account created and logged in",
                    user_id: result.insertId,
                    username: username,
                    email: email,
                    full_name: full_name,
                    user_img: user_img
                });
            }
        );
    });
});

// -----------------Transform PDF to Image------------------------

async function convertPdfToImages(pdfPath, quizId) {
    const outputDir = path.join(__dirname, "uploads", "quiz_pages", "quiz_" + quizId);

    if (!fs.existsSync(outputDir)) {
        fs.mkdirSync(outputDir, { recursive: true });
    }

    const options = {
        format: "png",
        out_dir: outputDir,
        out_prefix: "page",
        page: null
    };

    await pdfPoppler.convert(pdfPath, options);

    const files = fs.readdirSync(outputDir)
        .filter(file => file.endsWith(".png"))
        .sort();

    return files.map(file => path.join(outputDir, file));
}

function extractAnswerFromText(blockText) {
    const answerMatch = blockText.match(/(Đáp án|Answer|Correct Answer|Ans)\s*[:.]?\s*([A-D])/i);

    if (!answerMatch) {
        return null;
    }

    return answerMatch[2].toUpperCase();
}

function parseQuestionBlocksFromText(text) {
    const cleanedText = text
        .replace(/\r/g, "")
        .replace(/[ \t]+/g, " ")
        .trim();

    const blocks = cleanedText.split(/(?=Câu\s*\d+\s*[:.]|Question\s*\d+\s*[:.])/gi);

    const result = [];

    for (let i = 0; i < blocks.length; i++) {
        const block = blocks[i].trim();

        if (
            !/^Câu\s*\d+/i.test(block) &&
            !/^Question\s*\d+/i.test(block)
        ) {
            continue;
        }

        const orderMatch = block.match(/(?:Câu|Question)\s*(\d+)/i);
        const correctAnswer = extractAnswerFromText(block);

        if (!orderMatch || !correctAnswer) {
            continue;
        }

        result.push({
            question_order: parseInt(orderMatch[1]),
            correct_answer: correctAnswer
        });
    }

    return result;
}


// ----------------------------------------------------------------

function runCommand(command, args) {
    return new Promise((resolve, reject) => {
        execFile(command, args, (error, stdout, stderr) => {
            if (error) {
                reject(error);
            } else {
                resolve({ stdout, stderr });
            }
        });
    });
}

async function extractQuestionRegionsFromPdf(pdfPath, quizId) {
    const bboxDir = path.join(__dirname, "uploads", "quiz_bbox");

    if (!fs.existsSync(bboxDir)) {
        fs.mkdirSync(bboxDir, { recursive: true });
    }

    const htmlPath = path.join(bboxDir, "quiz_" + quizId + "_bbox.html");

    await runCommand("pdftotext", [
        "-bbox",
        pdfPath,
        htmlPath
    ]);

    const html = fs.readFileSync(htmlPath, "utf8");

    const pageRegex = /<page[^>]*width="([^"]+)"[^>]*height="([^"]+)"[^>]*>([\s\S]*?)<\/page>/g;
    const wordRegex = /<word[^>]*xMin="([^"]+)"[^>]*yMin="([^"]+)"[^>]*xMax="([^"]+)"[^>]*yMax="([^"]*)">([\s\S]*?)<\/word>/g;

    const allWords = [];
    let pageMatch;
    let pageIndex = 0;

    while ((pageMatch = pageRegex.exec(html)) !== null) {
        pageIndex++;

        const pageWidth = parseFloat(pageMatch[1]);
        const pageHeight = parseFloat(pageMatch[2]);
        const pageContent = pageMatch[3];

        let wordMatch;

        while ((wordMatch = wordRegex.exec(pageContent)) !== null) {
            allWords.push({
                pageIndex: pageIndex,
                pageWidth: pageWidth,
                pageHeight: pageHeight,
                text: wordMatch[5]
                    .replace(/&amp;/g, "&")
                    .replace(/&lt;/g, "<")
                    .replace(/&gt;/g, ">")
                    .trim(),
                xMin: parseFloat(wordMatch[1]),
                yMin: parseFloat(wordMatch[2]),
                xMax: parseFloat(wordMatch[3]),
                yMax: parseFloat(wordMatch[4])
            });
        }
    }

    const starts = [];

    for (let i = 0; i < allWords.length; i++) {
        const current = allWords[i].text;
        const next = i + 1 < allWords.length ? allWords[i + 1].text : "";

        if (/^Câu$/i.test(current) && /^\d+[:.]?$/.test(next)) {
            starts.push({
                index: i,
                order: parseInt(next.replace(/[:.]/g, ""))
            });
        }

        if (/^Câu\s*\d+[:.]?$/i.test(current)) {
            const num = current.match(/\d+/);
            if (num) {
                starts.push({
                    index: i,
                    order: parseInt(num[0])
                });
            }
        }

        if (/^Question$/i.test(current) && /^\d+[:.]?$/.test(next)) {
            starts.push({
                index: i,
                order: parseInt(next.replace(/[:.]/g, ""))
            });
        }
    }

    const regions = [];

    for (let i = 0; i < starts.length; i++) {
        const start = starts[i];
        const nextStartIndex = i + 1 < starts.length ? starts[i + 1].index : allWords.length;

        let answerIndex = -1;
        let correctAnswer = null;

        for (let j = start.index; j < nextStartIndex; j++) {
            const text = allWords[j].text;

            const isVietnameseAnswer =
                /^Đáp$/i.test(text) &&
                j + 1 < allWords.length &&
                /^án[:.]?$/i.test(allWords[j + 1].text);

            const isEnglishAnswer =
                /^Answer[:.]?$/i.test(text) ||
                /^Ans[:.]?$/i.test(text) ||
                /^Correct$/i.test(text);

            if (isVietnameseAnswer || isEnglishAnswer) {
                answerIndex = j;

                for (let k = j; k < Math.min(j + 8, allWords.length); k++) {
                    const ans = allWords[k].text.match(/^[A-D]$/i);

                    if (ans) {
                        correctAnswer = ans[0].toUpperCase();
                        break;
                    }
                }

                break;
            }
        }

        if (answerIndex === -1 || correctAnswer === null) {
            continue;
        }

        const questionWords = allWords.slice(start.index, answerIndex);

        if (questionWords.length === 0) {
            continue;
        }

        const pages = [...new Set(questionWords.map(w => w.pageIndex))];

        const parts = [];

        for (let p = 0; p < pages.length; p++) {
            const currentPage = pages[p];
            const wordsOnPage = questionWords.filter(w => w.pageIndex === currentPage);

            if (wordsOnPage.length === 0) {
                continue;
            }

            const firstWord = wordsOnPage[0];

            const xMin = Math.min(...wordsOnPage.map(w => w.xMin));
            const yMin = Math.min(...wordsOnPage.map(w => w.yMin));
            const xMax = Math.max(...wordsOnPage.map(w => w.xMax));
            const yMax = Math.max(...wordsOnPage.map(w => w.yMax));

            parts.push({
                pageIndex: currentPage,
                pageWidth: firstWord.pageWidth,
                pageHeight: firstWord.pageHeight,
                xMin: xMin,
                yMin: yMin,
                xMax: xMax,
                yMax: yMax
            });
        }

        regions.push({
            question_order: start.order,
            correct_answer: correctAnswer,
            parts: parts
        });
    }

    const uniqueMap = new Map();

    for (let i = 0; i < regions.length; i++) {
        const r = regions[i];

        if (!uniqueMap.has(r.question_order)) {
            uniqueMap.set(r.question_order, r);
        }
    }

    return Array.from(uniqueMap.values())
        .sort((a, b) => a.question_order - b.question_order);
}

async function cropQuestionsFromRegions(pageImagePaths, regions, quizId) {
    const questionDir = path.join(__dirname, "uploads", "quiz_questions", "quiz_" + quizId);

    if (!fs.existsSync(questionDir)) {
        fs.mkdirSync(questionDir, { recursive: true });
    }

    const savedQuestions = [];

    for (let i = 0; i < regions.length; i++) {
        const r = regions[i];

        const croppedBuffers = [];

        for (let j = 0; j < r.parts.length; j++) {
            const part = r.parts[j];
            const pagePath = pageImagePaths[part.pageIndex - 1];

            if (!pagePath) {
                continue;
            }

            const metadata = await sharp(pagePath).metadata();

            const scaleX = metadata.width / part.pageWidth;
            const scaleY = metadata.height / part.pageHeight;

            const paddingX = 25;
            const paddingY = 18;

            let left = Math.floor(part.xMin * scaleX) - paddingX;
            let top = Math.floor(part.yMin * scaleY) - paddingY;
            let right = Math.ceil(part.xMax * scaleX) + paddingX;
            let bottom = Math.ceil(part.yMax * scaleY) + paddingY;

            if (left < 0) left = 0;
            if (top < 0) top = 0;
            if (right > metadata.width) right = metadata.width;
            if (bottom > metadata.height) bottom = metadata.height;

            const width = right - left;
            const height = bottom - top;

            if (width <= 0 || height <= 0) {
                continue;
            }

            const buffer = await sharp(pagePath)
                .extract({
                    left: left,
                    top: top,
                    width: width,
                    height: height
                })
                .png()
                .toBuffer();

            const bufferMeta = await sharp(buffer).metadata();

            croppedBuffers.push({
                input: buffer,
                width: bufferMeta.width,
                height: bufferMeta.height
            });
        }

        if (croppedBuffers.length === 0) {
            continue;
        }

        let finalBuffer;

        if (croppedBuffers.length === 1) {
            finalBuffer = croppedBuffers[0].input;
        } else {
            const finalWidth = Math.max(...croppedBuffers.map(b => b.width));
            const finalHeight = croppedBuffers.reduce((sum, b) => sum + b.height, 0);

            let currentTop = 0;
            const composites = [];

            for (let k = 0; k < croppedBuffers.length; k++) {
                composites.push({
                    input: croppedBuffers[k].input,
                    left: 0,
                    top: currentTop
                });

                currentTop += croppedBuffers[k].height;
            }

            finalBuffer = await sharp({
                create: {
                    width: finalWidth,
                    height: finalHeight,
                    channels: 3,
                    background: "white"
                }
            })
                .composite(composites)
                .png()
                .toBuffer();
        }

        const fileName = "question_" + r.question_order + ".png";
        const outputPath = path.join(questionDir, fileName);

        await sharp(finalBuffer)
            .png()
            .toFile(outputPath);

        savedQuestions.push({
            question_order: r.question_order,
            question_img: `/uploads/quiz_questions/quiz_${quizId}/${fileName}`,
            correct_answer: r.correct_answer
        });
    }

    return savedQuestions;
}

app.post("/api/quizzes/upload-pdf", uploadQuizPdf.single("quiz_pdf"), async (req, res) => {
    const {
        lesson_id,
        quiz_title,
        time_limit_minutes,
        open_time,
        close_time
    } = req.body;

    if (!lesson_id || !req.file) {
        return res.status(400).json({
            success: false,
            message: "Missing lesson_id or PDF file"
        });
    }

    const pdfUrl = `/uploads/quiz_pdfs/${req.file.filename}`;

    const insertQuizSql = `
        INSERT INTO quizzes
        (
            lesson_id,
            quiz_title,
            quiz_pdf_url,
            time_limit_minutes,
            open_time,
            close_time,
            extract_status
        )
        VALUES (?, ?, ?, ?, ?, ?, 'processing')
    `;

    db.query(
        insertQuizSql,
        [
            lesson_id,
            quiz_title || req.file.originalname,
            pdfUrl,
            time_limit_minutes || 60,
            open_time || null,
            close_time || null
        ],
        async (quizErr, quizResult) => {
            if (quizErr) {
                console.log(quizErr);

                return res.status(500).json({
                    success: false,
                    message: "Cannot save quiz PDF to database",
                    error: quizErr
                });
            }

            const quizId = quizResult.insertId;

            const insertQuestionSql = `
                INSERT INTO quiz_questions
                (
                    quiz_id,
                    question_order,
                    question_img,
                    correct_answer
                )
                VALUES (?, ?, ?, ?)
                ON DUPLICATE KEY UPDATE
                    question_img = VALUES(question_img),
                    correct_answer = VALUES(correct_answer)
            `;

            try {
                // const pdfBuffer = fs.readFileSync(req.file.path);
                // const pdfData = await pdfParse(pdfBuffer);

                // const questions = parseQuestionBlocksFromText(pdfData.text);

                const pdfBuffer = fs.readFileSync(req.file.path);

                const parser = new PDFParse({
                    data: pdfBuffer
                });

                const pdfData = await parser.getText();

                const pageImages = await convertPdfToImages(req.file.path, quizId);

                const regions = await extractQuestionRegionsFromPdf(req.file.path, quizId);

                if (regions.length === 0) {
                    db.query(
                        `
                        UPDATE quizzes
                        SET extract_status = 'failed',
                            extract_message = 'No question regions found in PDF'
                        WHERE quiz_id = ?
                        `,
                        [quizId]
                    );

                    return res.status(400).json({
                        success: false,
                        message: "PDF uploaded but no question regions were found",
                        quiz_id: quizId
                    });
                }

                const croppedQuestions = await cropQuestionsFromRegions(
                    pageImages,
                    regions,
                    quizId
                );

                for (let i = 0; i < croppedQuestions.length; i++) {
                    const q = croppedQuestions[i];

                    const insertQuestionSql = `
                        INSERT INTO quiz_questions
                        (
                            quiz_id,
                            question_order,
                            question_img,
                            correct_answer
                        )
                        VALUES (?, ?, ?, ?)
                    `;

                    await new Promise((resolve, reject) => {
                        db.query(
                            insertQuestionSql,
                            [
                                quizId,
                                q.question_order,
                                q.question_img,
                                q.correct_answer
                            ],
                            (err, result) => {
                                if (err) reject(err);
                                else resolve(result);
                            }
                        );
                    });
                }

                db.query(
                    `
                    UPDATE quizzes
                    SET extract_status = 'done',
                        total_questions = ?,
                        extracted_at = NOW()
                    WHERE quiz_id = ?
                    `,
                    [croppedQuestions.length, quizId]
                );

                res.json({
                    success: true,
                    message: "Quiz PDF uploaded, converted, cropped and saved successfully",
                    quiz_id: quizId,
                    quiz_pdf_url: pdfUrl,
                    total_questions: croppedQuestions.length,
                    questions: croppedQuestions
                });

            } catch (err) {
                console.log(err);

                db.query(
                    `
                    UPDATE quizzes
                    SET extract_status = 'failed',
                        extract_message = ?
                    WHERE quiz_id = ?
                    `,
                    [err.message, quizId]
                );

                res.status(500).json({
                    success: false,
                    message: "PDF uploaded but extraction failed",
                    quiz_id: quizId,
                    error: err.message
                });
            }
        }
    );
});

app.get("/api/quizzes/:quizId/questions", (req, res) => {
    const quizId = req.params.quizId;

    const sql = `
        SELECT 
            question_id,
            quiz_id,
            question_order,
            question_img
        FROM quiz_questions
        WHERE quiz_id = ?
        ORDER BY question_order ASC
    `;

    db.query(sql, [quizId], (err, results) => {
        if (err) {
            console.log(err);

            return res.status(500).json({
                success: false,
                message: "Cannot get quiz questions"
            });
        }

        res.json({
            success: true,
            questions: results
        });
    });
});

app.post("/api/quizzes/:quizId/submit", (req, res) => {
    const quizId = parseInt(req.params.quizId);
    const { student_id, duration_seconds, answers } = req.body;

    if (!student_id || !Array.isArray(answers)) {
        return res.status(400).json({
            success: false,
            message: "Missing student_id or answers"
        });
    }

    db.beginTransaction((err) => {
        if (err) {
            return res.status(500).json({ success: false, message: err.message });
        }

        const attemptNumberSql = `
            SELECT COALESCE(MAX(attempt_number), 0) + 1 AS next_attempt_number
            FROM quiz_attempts
            WHERE quiz_id = ? AND student_id = ?
        `;

        db.query(attemptNumberSql, [quizId, student_id], (err, attemptRows) => {
            if (err) return rollback(res, err);

            const attemptNumber = attemptRows[0].next_attempt_number;

            const questionSql = `
                SELECT question_id, correct_answer
                FROM quiz_questions
                WHERE quiz_id = ?
            `;

            db.query(questionSql, [quizId], (err, questionRows) => {
                if (err) return rollback(res, err);

                const correctMap = {};
                for (let i = 0; i < questionRows.length; i++) {
                    correctMap[questionRows[i].question_id] = questionRows[i].correct_answer;
                }

                let correctCount = 0;

                for (let i = 0; i < answers.length; i++) {
                    const questionId = answers[i].question_id;
                    const selectedAnswer = answers[i].selected_answer;

                    if (correctMap[questionId] === selectedAnswer) {
                        correctCount++;
                    }
                }

                const totalQuestions = questionRows.length;
                const score = totalQuestions > 0 ? (correctCount / totalQuestions) * 10 : 0;

                const insertAttemptSql = `
                    INSERT INTO quiz_attempts
                    (
                        quiz_id, student_id, attempt_number,
                        submitted_at, duration_seconds,
                        total_questions, correct_count, score, status
                    )
                    VALUES (?, ?, ?, NOW(), ?, ?, ?, ?, 'submitted')
                `;

                db.query(
                    insertAttemptSql,
                    [
                        quizId,
                        student_id,
                        attemptNumber,
                        duration_seconds,
                        totalQuestions,
                        correctCount,
                        score
                    ],
                    (err, attemptResult) => {
                        if (err) return rollback(res, err);

                        const attemptId = attemptResult.insertId;

                        if (answers.length === 0) {
                            return commitSubmit(res, attemptId, attemptNumber, correctCount, totalQuestions, score);
                        }

                        const values = [];

                        for (let i = 0; i < answers.length; i++) {
                            const questionId = answers[i].question_id;
                            const selectedAnswer = answers[i].selected_answer;
                            const isCorrect = correctMap[questionId] === selectedAnswer;

                            values.push([
                                attemptId,
                                questionId,
                                selectedAnswer,
                                isCorrect
                            ]);
                        }

                        const insertAnswersSql = `
                            INSERT INTO quiz_attempt_answers
                            (attempt_id, question_id, selected_answer, is_correct)
                            VALUES ?
                        `;

                        db.query(insertAnswersSql, [values], (err) => {
                            if (err) return rollback(res, err);

                            commitSubmit(res, attemptId, attemptNumber, correctCount, totalQuestions, score);
                        });
                    }
                );
            });
        });
    });
});

app.get("/api/quizzes/:quizId/students/:studentId/attempts", (req, res) => {
    const quizId = parseInt(req.params.quizId);
    const studentId = parseInt(req.params.studentId);

    const sql = `
        SELECT 
            attempt_id,
            attempt_number,
            started_at,
            submitted_at,
            duration_seconds,
            total_questions,
            correct_count,
            score,
            status
        FROM quiz_attempts
        WHERE quiz_id = ? AND student_id = ?
        ORDER BY attempt_number ASC
    `;

    db.query(sql, [quizId, studentId], (err, rows) => {
        if (err) {
            return res.status(500).json({
                success: false,
                message: err.message
            });
        }

        res.json({
            success: true,
            attempts: rows
        });
    });
});

function rollback(res, err) {
    db.rollback(() => {
        res.status(500).json({
            success: false,
            message: err.message
        });
    });
}

function commitSubmit(res, attemptId, attemptNumber, correctCount, totalQuestions, score) {
    db.commit((err) => {
        if (err) {
            return rollback(res, err);
        }

        res.json({
            success: true,
            attempt_id: attemptId,
            attempt_number: attemptNumber,
            correct_count: correctCount,
            total_questions: totalQuestions,
            score: score
        });
    });
}

// -------------------API upload PDF bài học-------------
app.post("/api/lessons/:lessonId/upload-pdf", lessonPdfUpload.single("pdf_file"), (req, res) => {
    const lessonId = req.params.lessonId;

    if (!req.file) {
        return res.status(400).json({
            success: false,
            message: "No PDF file uploaded"
        });
    }

    const pdfFileUrl = `/uploads/lesson_pdfs/${req.file.filename}`;

    const sql = `
        UPDATE lessons
        SET pdf_file_url = ?
        WHERE lesson_id = ?
    `;

    db.query(sql, [pdfFileUrl, lessonId], (err, result) => {
        if (err) {
            console.error("Upload lesson PDF error:", err);
            return res.status(500).json({
                success: false,
                message: "Database error"
            });
        }

        if (result.affectedRows === 0) {
            return res.status(404).json({
                success: false,
                message: "Lesson not found"
            });
        }

        return res.json({
            success: true,
            message: "Upload lesson PDF successfully",
            pdf_file_url: pdfFileUrl
        });
    });
});
// -------------------------------------------------------------

// --------------------API show lesson PDF -------------------------
app.get("/api/lessons/:lessonId", (req, res) => {
    const lessonId = req.params.lessonId;

    const sql = `
        SELECT lesson_id, class_id, lesson_title, lesson_number,
               lesson_description, lesson_img, pdf_file_url, created_at
        FROM lessons
        WHERE lesson_id = ?
    `;

    db.query(sql, [lessonId], (err, results) => {
        if (err) {
            console.error("Get lesson error:", err);
            return res.status(500).json({
                success: false,
                message: "Database error"
            });
        }

        if (results.length === 0) {
            return res.status(404).json({
                success: false,
                message: "Lesson not found"
            });
        }

        return res.json({
            success: true,
            lesson: results[0]
        });
    });
});
// -------------------------------------------------------------

app.get("/api/teachers/:teacherId/classes", (req, res) => {
    const teacherId = req.params.teacherId;

    const sql = `
        SELECT 
            c.class_id,
            c.class_name,
            c.summary_info,
            c.class_img,
            c.teacher_id,
            u.full_name AS teacher_name,
            u.user_img AS teacher_img,
            c.created_at
        FROM classes c
        JOIN users u ON c.teacher_id = u.user_id
        WHERE c.teacher_id = ?
        ORDER BY c.created_at DESC
    `;

    db.query(sql, [teacherId], (err, results) => {
        if (err) {
            console.error(err);
            return res.status(500).json({ success: false, message: "Database error" });
        }

        res.json({ success: true, classes: results });
    });
});


// ===============================
// GET CLASSES BY STUDENT
// ===============================
app.get("/api/students/:studentId/classes", (req, res) => {
    const studentId = req.params.studentId;

    const sql = `
        SELECT 
            c.class_id,
            c.class_name,
            c.summary_info,
            c.class_img,
            c.teacher_id,
            u.full_name AS teacher_name,
            u.user_img AS teacher_img,
            c.created_at
        FROM class_enrollments ce
        JOIN classes c ON ce.class_id = c.class_id
        JOIN users u ON c.teacher_id = u.user_id
        WHERE ce.student_id = ?
        ORDER BY ce.joined_at DESC
    `;

    db.query(sql, [studentId], (err, results) => {
        if (err) {
            console.error(err);
            return res.status(500).json({ success: false, message: "Database error" });
        }

        res.json({ success: true, classes: results });
    });
});
// --------------------------------------------------------

// --------------Delete Class from Teachers-----------------------
app.delete("/api/classes/:classId", (req, res) => {
    const classId = req.params.classId;

    const checkStudentsSql = `
        SELECT 
            ce.student_id,
            u.full_name,
            u.email
        FROM class_enrollments ce
        JOIN users u ON ce.student_id = u.user_id
        WHERE ce.class_id = ?
    `;

    db.query(checkStudentsSql, [classId], (err, students) => {
        if (err) {
            console.error("Check students error:", err);
            return res.status(500).json({
                success: false,
                message: "Lỗi khi kiểm tra học sinh trong lớp"
            });
        }

        const deleteClassSql = "DELETE FROM classes WHERE class_id = ?";

        db.query(deleteClassSql, [classId], (err, result) => {
            if (err) {
                console.error("Delete class error:", err);
                return res.status(500).json({
                    success: false,
                    message: "Lỗi khi xóa lớp học"
                });
            }

            if (result.affectedRows === 0) {
                return res.status(404).json({
                    success: false,
                    message: "Không tìm thấy lớp học"
                });
            }

            res.json({
                success: true,
                message: "Xóa lớp học thành công",
                removed_students_count: students.length,
                removed_students: students
            });
        });
    });
});
// ----------------------------------------------------

// --------------------Cancel Class from Students------------
app.delete("/api/students/:studentId/classes/:classId", (req, res) => {
    const studentId = req.params.studentId;
    const classId = req.params.classId;

    const sql = `
        DELETE FROM class_enrollments
        WHERE student_id = ? AND class_id = ?
    `;

    db.query(sql, [studentId, classId], (err, result) => {
        if (err) {
            console.error("Cancel enrollment error:", err);
            return res.status(500).json({
                success: false,
                message: "Lỗi khi hủy đăng kí lớp học"
            });
        }

        if (result.affectedRows === 0) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy đăng kí lớp học"
            });
        }

        res.json({
            success: true,
            message: "Hủy đăng kí lớp học thành công"
        });
    });
});
// ----------------------------------------------------------

// ----------------------Add Class from Teachers----------------

// ---- Upload class image-----

const image_class_storage = multer.diskStorage({
    destination: function (req, file, cb) {
        cb(null, classImageDir);
    },
    filename: function (req, file, cb) {
        const uniqueName = Date.now() + "-" + file.originalname;
        cb(null, uniqueName);
    }
});

const image_class_upload = multer({
    storage: image_class_storage
});

app.post(
    "/api/upload/class-image",
    image_class_upload.single("class_img"),
    (req, res) => {
        if (!req.file) {
            return res.status(400).json({
                success: false,
                message: "Không có file ảnh"
            });
        }

        const imageUrl =
            "http://localhost:3000/uploads/class_images/" + req.file.filename;

        res.json({
            success: true,
            image_url: imageUrl
        });
    }
);
// ------------------------------------

app.post("/api/classes", (req, res) => {
    const { class_name, summary_info, class_img, teacher_id } = req.body;

    if (!class_name || !summary_info || !teacher_id) {
        return res.status(400).json({
            success: false,
            message: "Thiếu thông tin lớp học"
        });
    }

    const sql = `
        INSERT INTO classes (class_name, summary_info, class_img, teacher_id)
        VALUES (?, ?, ?, ?)
    `;

    db.query(sql, [class_name, summary_info, class_img || null, teacher_id], (err, result) => {
        if (err) {
            console.error("Create class error:", err);
            return res.status(500).json({
                success: false,
                message: "Lỗi khi tạo lớp học"
            });
        }

        res.json({
            success: true,
            message: "Tạo lớp học thành công",
            class_id: result.insertId
        });
    });
});
// ------------------------------------------------------

// -----------Queries All Class in Database
app.get("/api/students/:studentId/available-classes", (req, res) => {
    const studentId = req.params.studentId;

    const sql = `
        SELECT 
            c.class_id,
            c.class_name,
            c.summary_info,
            c.class_img,
            c.teacher_id,
            u.full_name AS teacher_name,
            u.user_img AS teacher_img
        FROM classes c
        JOIN users u ON c.teacher_id = u.user_id
        WHERE c.teacher_id != ?
        AND c.class_id NOT IN (
            SELECT class_id 
            FROM class_enrollments 
            WHERE student_id = ?
        )
        ORDER BY c.created_at DESC
    `;

    db.query(sql, [studentId, studentId], (err, results) => {
        if (err) {
            console.error("Get available classes error:", err);
            return res.status(500).json({
                success: false,
                message: "Lỗi lấy danh sách lớp có thể đăng kí"
            });
        }

        res.json({
            success: true,
            classes: results
        });
    });
});
// --------------------------------------------------

// -------------Assign a Class from Student----------------
app.post("/api/students/:studentId/classes/:classId", (req, res) => {
    const studentId = req.params.studentId;
    const classId = req.params.classId;

    const sql = `
        INSERT INTO class_enrollments (student_id, class_id)
        VALUES (?, ?)
    `;

    db.query(sql, [studentId, classId], (err, result) => {
        if (err) {
            console.error("Enroll class error:", err);
            return res.status(500).json({
                success: false,
                message: "Lỗi đăng kí lớp học"
            });
        }

        res.json({
            success: true,
            message: "Đăng kí lớp học thành công"
        });
    });
});
// ------------------------------------------------------

// ---------- Lấy Lesson trong Class ---------------------
app.get("/api/lessons/class/:classId", (req, res) => {
    const classId = req.params.classId;

    const sql = `
        SELECT 
            l.lesson_id,
            l.class_id,
            l.lesson_title,
            l.lesson_number,
            l.lesson_description,
            l.lesson_img,
            l.pdf_file_url,
            l.created_at,

            u.full_name AS teacher_name,
            u.user_img AS teacher_img
        FROM lessons l
        JOIN classes c ON l.class_id = c.class_id
        JOIN users u ON c.teacher_id = u.user_id
        WHERE l.class_id = ?
        ORDER BY l.created_at ASC, l.lesson_id ASC
    `;

    db.query(sql, [classId], (err, results) => {
        if (err) {
            console.error(err);
            return res.status(500).json({
                success: false,
                message: "Database error"
            });
        }

        res.json({
            success: true,
            lessons: results
        });
    });
});
// ---------------------------------------------------

// ----------------Create New Lesson-------------------------

// ---------Cấu hình upload lesson-----
const lessonUploadDir = path.join(__dirname, "uploads", "lesson_files");

if (!fs.existsSync(lessonUploadDir)) {
    fs.mkdirSync(lessonUploadDir, { recursive: true });
}

const lessonUploadStorage = multer.diskStorage({
    destination: function (req, file, cb) {
        cb(null, lessonUploadDir);
    },

    filename: function (req, file, cb) {

        let originalName = file.originalname;

        // Decode UTF8 nếu bị encode kiểu MIME
        try {
            originalName = Buffer.from(originalName, "latin1").toString("utf8");
        } catch (e) {
            console.log("Decode filename error:", e);
        }

        // Xóa chuỗi MIME encode kiểu =?utf-8?Q?...?=
        originalName = originalName
            .replace(/=\?utf-8\?Q\?/gi, "")
            .replace(/\?=/g, "");

        // Lấy extension
        const ext = path.extname(originalName) || ".pdf";

        // Làm sạch tên file
        const baseName = path
            .basename(originalName, ext)
            .normalize("NFD")
            .replace(/[\u0300-\u036f]/g, "") // bỏ dấu tiếng Việt
            .replace(/[^a-zA-Z0-9_-]/g, "_") // ký tự lạ -> _
            .replace(/_+/g, "_") // nhiều _ -> 1 _
            .replace(/^_+|_+$/g, ""); // bỏ _ đầu/cuối

        const uniqueName =
            Date.now() + "-" + baseName + ext.toLowerCase();

        console.log("Saved filename:", uniqueName);

        cb(null, uniqueName);
    }
});

const lessonUpload = multer({
    storage: lessonUploadStorage,

    fileFilter: function (req, file, cb) {

        // ================= PDF =================

        if (
            file.fieldname === "lesson_pdf" ||
            file.fieldname === "exercise_pdf"
        ) {
            if (file.mimetype === "application/pdf") {
                cb(null, true);
            }
            else {
                cb(new Error(
                    "lesson_pdf và exercise_pdf phải là file PDF"
                ));
            }
        }

        // ================= IMAGE =================

        else if (file.fieldname === "background_image") {

            if (file.mimetype.startsWith("image/")) {
                cb(null, true);
            }
            else {
                cb(new Error(
                    "background_image phải là file ảnh"
                ));
            }
        }

        // ================= MODEL OBJ =================
        // Mở phần này sau khi cần upload model

        /*
        else if (file.fieldname === "model_files") {

            const lowerName = file.originalname.toLowerCase();

            if (
                lowerName.endsWith(".obj") ||
                lowerName.endsWith(".glb") ||
                lowerName.endsWith(".gltf")
            ) {
                cb(null, true);
            }
            else {
                cb(new Error(
                    "model_files phải là .obj, .glb hoặc .gltf"
                ));
            }
        }
        */

        // ================= UNKNOWN =================

        else {
            cb(new Error(
                "Unexpected field: " + file.fieldname
            ));
        }
    }
});
// ---------------------------------------

app.post(
    "/api/lessons/create",
    lessonUpload.fields([
        { name: "lesson_pdf", maxCount: 1 },
        { name: "exercise_pdf", maxCount: 1 },
        { name: "background_image", maxCount: 1 }

        // Sau này mở lại khi upload model obj
        // { name: "model_files", maxCount: 10 }
    ]),
    async (req, res) => {
        try {
            const body = req.body || {};

            console.log("Create lesson body:", body);
            console.log("Create lesson files:", req.files);

            const {
                class_id,
                lesson_title,
                lesson_info,
                time_exercise,
                deadline_date,
                deadline_time
            } = body;

            if (!class_id || !lesson_title) {
                return res.status(400).json({
                    success: false,
                    message: "Thiếu class_id hoặc lesson_title"
                });
            }

            const baseUrl = `${req.protocol}://${req.get("host")}`;

            let lessonImgUrl = null;
            let lessonPdfUrl = null;
            let exercisePdfUrl = null;

            if (req.files && req.files.background_image) {
                lessonImgUrl =
                    `${baseUrl}/uploads/lesson_files/${req.files.background_image[0].filename}`;
            }

            if (req.files && req.files.lesson_pdf) {
                lessonPdfUrl =
                    `${baseUrl}/uploads/lesson_files/${req.files.lesson_pdf[0].filename}`;
            }

            if (req.files && req.files.exercise_pdf) {
                exercisePdfUrl =
                    `${baseUrl}/uploads/lesson_files/${req.files.exercise_pdf[0].filename}`;
            }

            const [countRows] = await db.promise().query(
                `SELECT COUNT(*) AS total
                FROM lessons
                WHERE class_id = ?`,
                [class_id]
            );

            const lessonNumber = countRows[0].total + 1;

            const [lessonResult] = await db.promise().query(
                `INSERT INTO lessons
                (
                    class_id,
                    lesson_title,
                    lesson_number,
                    lesson_description,
                    lesson_img,
                    pdf_file_url
                )
                VALUES (?, ?, ?, ?, ?, ?)`,
                [
                    class_id,
                    lesson_title,
                    lessonNumber,
                    lesson_info || null,
                    lessonImgUrl,
                    lessonPdfUrl
                ]
            );

            const lessonId = lessonResult.insertId;

            // ================= MODEL FILES =================
            // Sau này nếu mở upload model obj thì bỏ comment đoạn này

            /*
            if (req.files && req.files.model_files) {
                for (let i = 0; i < req.files.model_files.length; i++) {
                    const modelFile = req.files.model_files[i];

                    const modelUrl =
                        `${baseUrl}/uploads/lesson_files/${modelFile.filename}`;

                    await db.promise().query(
                        `INSERT INTO models
                        (lesson_id, model_title, model_url)
                        VALUES (?, ?, ?)`,
                        [
                            lessonId,
                            modelFile.originalname,
                            modelUrl
                        ]
                    );
                }
            }
            */

            // ================= EXERCISE / QUIZ =================

            if (exercisePdfUrl) {
                let closeTime = null;

                if (deadline_date) {
                    const parts = deadline_date.split("/");

                    if (parts.length === 3) {
                        const day = parts[0];
                        const month = parts[1];
                        const year = parts[2];

                        let timeValue = "23:59:00";

                        if (deadline_time) {
                            timeValue = deadline_time;
                        }

                        closeTime = `${year}-${month}-${day} ${timeValue}`;
                    }
                }

                let timeLimitMinutes = 60;

                if (time_exercise) {
                    const parsedMinute = parseInt(time_exercise);

                    if (!isNaN(parsedMinute)) {
                        timeLimitMinutes = parsedMinute;
                    }
                }

                await db.promise().query(
                    `INSERT INTO quizzes
                    (lesson_id, quiz_title, quiz_pdf_url, time_limit_minutes, close_time)
                    VALUES (?, ?, ?, ?, ?)`,
                    [
                        lessonId,
                        lesson_title + " - Bài tập",
                        exercisePdfUrl,
                        timeLimitMinutes,
                        closeTime
                    ]
                );
            }

            return res.json({
                success: true,
                message: "Tạo bài học thành công",
                lesson_id: lessonId,
                lesson_img: lessonImgUrl,
                lesson_pdf: lessonPdfUrl,
                exercise_pdf: exercisePdfUrl
            });

        } catch (error) {
            console.error("Create lesson error:", error);

            return res.status(500).json({
                success: false,
                message: "Lỗi server khi tạo bài học",
                error: error.message
            });
        }
    }
);
// -----------------------------------------------

// ------------------Delete Lesson--------------------
app.delete("/api/lessons/:lessonId", (req, res) => {
    const lessonId = req.params.lessonId;

    const sql = `
        DELETE FROM lessons
        WHERE lesson_id = ?
    `;

    db.query(sql, [lessonId], (err, result) => {
        if (err) {
            console.error("Delete lesson error:", err);

            return res.status(500).json({
                success: false,
                message: "Lỗi khi xóa bài học"
            });
        }

        if (result.affectedRows === 0) {
            return res.status(404).json({
                success: false,
                message: "Không tìm thấy bài học"
            });
        }

        res.json({
            success: true,
            message: "Xóa bài học thành công"
        });
    });
});
// ------------------------------------

// -----------Update info lesson--------------
app.put(
    "/api/lessons/:lessonId",
    lessonUpload.fields([
        { name: "lesson_pdf", maxCount: 1 },
        { name: "exercise_pdf", maxCount: 1 },
        { name: "background_image", maxCount: 1 }
    ]),
    async (req, res) => {
        try {
            const lessonId = req.params.lessonId;
            const body = req.body || {};

            console.log("Update lesson body:", body);
            console.log("Update lesson files:", req.files);

            const lesson_title = body.lesson_title || body.lesson_name;
            const lesson_info = body.lesson_info || body.lesson_description;

            if (!lesson_title) {
                return res.status(400).json({
                    success: false,
                    message: "Thiếu lesson_title"
                });
            }

            const baseUrl = `${req.protocol}://${req.get("host")}`;

            let lessonImgUrl = null;
            let lessonPdfUrl = null;

            if (req.files && req.files.background_image) {
                lessonImgUrl =
                    `${baseUrl}/uploads/lesson_files/${req.files.background_image[0].filename}`;
            }

            if (req.files && req.files.lesson_pdf) {
                lessonPdfUrl =
                    `${baseUrl}/uploads/lesson_files/${req.files.lesson_pdf[0].filename}`;
            }

            let sql = `
                UPDATE lessons
                SET lesson_title = ?,
                    lesson_description = ?
            `;

            const values = [
                lesson_title,
                lesson_info || null
            ];

            if (lessonImgUrl) {
                sql += `, lesson_img = ?`;
                values.push(lessonImgUrl);
            }

            if (lessonPdfUrl) {
                sql += `, pdf_file_url = ?`;
                values.push(lessonPdfUrl);
            }

            sql += ` WHERE lesson_id = ?`;
            values.push(lessonId);

            const [result] = await db.promise().query(sql, values);

            if (result.affectedRows === 0) {
                return res.status(404).json({
                    success: false,
                    message: "Không tìm thấy bài học"
                });
            }

            res.json({
                success: true,
                message: "Cập nhật bài học thành công"
            });

        } catch (error) {
            console.error("Update lesson error:", error);

            res.status(500).json({
                success: false,
                message: "Lỗi server khi cập nhật bài học",
                error: error.message
            });
        }
    }
);
// -----------------------------------------


app.listen(process.env.PORT, "0.0.0.0", () => {
    console.log(`Server is running on http://localhost:${process.env.PORT}`);
});