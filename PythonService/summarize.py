import torch
from transformers import T5ForConditionalGeneration, T5Tokenizer

# ── Configuration ─────────────────────────────────────────────────────────────
MODEL_DIR         = "/home/mostafa/Desktop/sumrmize/Model"
MAX_INPUT_LENGTH  = 512
MAX_TARGET_LENGTH = 128

# ── Load model & tokenizer ────────────────────────────────────────────────────
print("Loading model...")
device    = "cuda" if torch.cuda.is_available() else "cpu"
# summarize.py
tokenizer = T5Tokenizer.from_pretrained(MODEL_DIR, local_files_only=True)
model     = T5ForConditionalGeneration.from_pretrained(MODEL_DIR, local_files_only=True).to(device)
model.eval()
print(f"Model loaded on {device}\n")


# ── Summarize a single chunk ──────────────────────────────────────────────────
def summarize_chunk(text: str) -> str:
    input_text = "summarize: " + text.strip()

    inputs = tokenizer(
        input_text,
        return_tensors="pt",
        max_length=MAX_INPUT_LENGTH,
        truncation=True,
    ).to(device)

    with torch.no_grad():
        output_ids = model.generate(
            inputs["input_ids"],
            max_new_tokens=MAX_TARGET_LENGTH,
            min_new_tokens=10,
            num_beams=4,
            length_penalty=2.0,
            early_stopping=True,
            no_repeat_ngram_size=3,   # prevents repeating any 3-word phrase
            repetition_penalty=2.5,   # penalizes repeated tokens heavily
        )

    return tokenizer.decode(output_ids[0], skip_special_tokens=True)


# ── Chunk + Summarize the full text ──────────────────────────────────────────
def summarize_long_text(text: str, chunk_size: int = 350) -> list[str]:
    words  = text.split()
    chunks = [" ".join(words[i:i + chunk_size]) for i in range(0, len(words), chunk_size)]

    print(f"Total words  : {len(words)}")
    print(f"Chunk size   : {chunk_size} words")
    print(f"Total chunks : {len(chunks)}\n")

    summaries = []
    for i, chunk in enumerate(chunks):
        token_count = len(tokenizer.encode(chunk))
        print(f"Chunk {i + 1}/{len(chunks)} — {len(chunk.split())} words / {token_count} tokens")

        if token_count > MAX_INPUT_LENGTH:
            print(f"  WARNING: exceeds {MAX_INPUT_LENGTH} tokens, will be truncated.")

        summary = summarize_chunk(chunk)
        summaries.append(summary)
        

    return summaries


# ── Main ──────────────────────────────────────────────────────────────────────
if __name__ == "__main__":
    text = """

"""

    summaries = summarize_long_text(text)

    print("=" * 50)
    print("        Final Summaries per Chunk")
    print("=" * 50)
    for i, summary in enumerate(summaries):
        print(f"\n")
        print(summary)
    print("\n" + "=" * 50)