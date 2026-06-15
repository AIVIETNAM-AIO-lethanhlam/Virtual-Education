const admin = require("firebase-admin");
const serviceAccount = require("./firebase-service-account.json");

admin.initializeApp({
    credential: admin.credential.cert(serviceAccount),
    storageBucket: "virtual-education-d056a.firebasestorage.app"
});

const db = admin.firestore();
const bucket = admin.storage().bucket();

module.exports = {
    admin,
    db,
    bucket
};