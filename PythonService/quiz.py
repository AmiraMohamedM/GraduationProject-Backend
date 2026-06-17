import os
import json
from google import genai
from google.genai import types
from dotenv import load_dotenv

load_dotenv()

def generate_quiz(text: str, num_questions: int) -> list[dict]:
    client = genai.Client(
        api_key=os.environ.get("GEMINI_API_KEY"),
    )

    model = "gemini-3.1-flash-lite"

    prompt = f"""You are a quiz generator. Given the following text, generate exactly {num_questions} multiple choice questions.

Return ONLY a valid JSON array with no extra text, no markdown, no code blocks.

Each object in the array must have exactly these fields:
- "question": the question string
- "options": an array of exactly 4 answer strings (A, B, C, D)
- "answer": the correct option string (must match one of the options exactly)

Text:
{text}"""

    contents = [
        types.Content(
            role="user",
            parts=[types.Part.from_text(text=prompt)],
        ),
    ]

    generate_content_config = types.GenerateContentConfig(
        thinking_config=types.ThinkingConfig(
            thinking_level="MEDIUM",
        ),
    )

    raw = ""
    for chunk in client.models.generate_content_stream(
        model=model,
        contents=contents,
        config=generate_content_config,
    ):
        if chunk.text:
            raw += chunk.text

    # Strip markdown code fences if model adds them anyway
    raw = raw.strip()
    if raw.startswith("```"):
        raw = raw.split("```")[1]
        if raw.startswith("json"):
            raw = raw[4:]
    raw = raw.strip()

    return json.loads(raw)


if __name__ == "__main__":
    sample_text = """

    """

    num_questions = 5

    quiz = generate_quiz(sample_text, num_questions)
    print(json.dumps(quiz, indent=2))