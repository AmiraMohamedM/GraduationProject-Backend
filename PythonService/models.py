import io
import json

import pdfplumber
from fastapi import FastAPI, File, UploadFile, Form, HTTPException
from fastapi.responses import JSONResponse

from summarize import summarize_long_text
from quiz import generate_quiz
from true_false import generate_true_false

app = FastAPI(title="PDF Summarizer & Quiz Generator")


# ── Shared Helper ──────────────────────────────────────────────────────────────

def extract_text_from_pdf(file_bytes: bytes) -> str:
    with pdfplumber.open(io.BytesIO(file_bytes)) as pdf:
        return "\n\n".join(page.extract_text() or "" for page in pdf.pages)


# ── Endpoints ──────────────────────────────────────────────────────────────────

@app.post("/summarize")
async def summarize_pdf(file: UploadFile = File(...)):
    """Upload a PDF → single summary text."""
    if not file.filename.lower().endswith(".pdf"):
        raise HTTPException(status_code=400, detail="Only PDF files are accepted.")

    text = extract_text_from_pdf(await file.read())

    if not text.strip():
        raise HTTPException(status_code=422, detail="No text could be extracted from the PDF.")

    summaries = summarize_long_text(text)

    return JSONResponse(content={
        "filename": file.filename,
        "summary": "\n\n".join(summaries),
    })


@app.post("/quiz")
async def quiz_pdf(
    file: UploadFile = File(...),
    num_questions: int = Form(default=5),
):
    """Upload a PDF + question count → MCQ quiz as JSON."""
    if not file.filename.lower().endswith(".pdf"):
        raise HTTPException(status_code=400, detail="Only PDF files are accepted.")

    if not (1 <= num_questions <= 50):
        raise HTTPException(status_code=400, detail="num_questions must be between 1 and 50.")

    text = extract_text_from_pdf(await file.read())

    if not text.strip():
        raise HTTPException(status_code=422, detail="No text could be extracted from the PDF.")

    try:
        quiz = generate_quiz(text, num_questions)
    except json.JSONDecodeError as e:
        raise HTTPException(status_code=500, detail=f"Gemini returned invalid JSON: {e}")

    return JSONResponse(content={
        "filename": file.filename,
        "num_questions": len(quiz),
        "quiz": quiz,
    })


@app.post("/true-false")
async def true_false_pdf(
    file: UploadFile = File(...),
    num_questions: int = Form(default=5),
):
    """Upload a PDF + question count → true/false questions as JSON."""
    if not file.filename.lower().endswith(".pdf"):
        raise HTTPException(status_code=400, detail="Only PDF files are accepted.")

    if not (1 <= num_questions <= 50):
        raise HTTPException(status_code=400, detail="num_questions must be between 1 and 50.")

    text = extract_text_from_pdf(await file.read())

    if not text.strip():
        raise HTTPException(status_code=422, detail="No text could be extracted from the PDF.")

    try:
        questions = generate_true_false(text, num_questions)
    except json.JSONDecodeError as e:
        raise HTTPException(status_code=500, detail=f"Gemini returned invalid JSON: {e}")

    return JSONResponse(content={
        "filename": file.filename,
        "num_questions": len(questions),
        "questions": questions,
    })