#!/usr/bin/env python3
"""
read_pdf.py — Extract all text from a PDF file.

Edit the PDF_PATH variable below to point to your PDF.
"""

import sys
from pathlib import Path

# ── Configuration ────────────────────────────────────────────
PDF_PATH = "/run/media/mostafa/New Volume/sumrmize/Model/CH-1.pdf"   # <-- set your PDF path here
OUTPUT   = "result.txt"             # set to a filename to save, e.g. "result.txt"
PAGES    = None             # set to a range string, e.g. "1-5" or "1,3,7-10"
ENGINE   = "pdfplumber"     # "pdfplumber" or "pypdf"
# ─────────────────────────────────────────────────────────────


def extract_text_pypdf(pdf_path: str) -> list[str]:
    """Extract text page-by-page using pypdf."""
    from pypdf import PdfReader
    reader = PdfReader(pdf_path)
    return [page.extract_text() or "" for page in reader.pages]


def extract_text_pdfplumber(pdf_path: str) -> list[str]:
    """Extract text page-by-page using pdfplumber (better layout handling)."""
    import pdfplumber
    pages = []
    with pdfplumber.open(pdf_path) as pdf:
        for page in pdf.pages:
            pages.append(page.extract_text() or "")
    return pages


def parse_page_range(spec: str, total: int) -> list[int]:
    """Parse a page range like '1-5,7,10-12' into a list of 0-based indices."""
    indices = set()
    for part in spec.split(","):
        part = part.strip()
        if "-" in part:
            start, end = part.split("-", 1)
            indices.update(range(int(start) - 1, int(end)))
        else:
            indices.add(int(part) - 1)
    return sorted(i for i in indices if 0 <= i < total)


def main():
    pdf_path = PDF_PATH

    if not Path(pdf_path).exists():
        print(f"Error: file not found — {pdf_path}", file=sys.stderr)
        sys.exit(1)

    # Extract text
    print(f"Reading '{pdf_path}' with {ENGINE}…")
    if ENGINE == "pypdf":
        pages = extract_text_pypdf(pdf_path)
    else:
        pages = extract_text_pdfplumber(pdf_path)

    total_pages = len(pages)
    print(f"Total pages: {total_pages}")

    # Filter pages if requested
    if PAGES:
        indices = parse_page_range(PAGES, total_pages)
        pages = [pages[i] for i in indices]
        print(f"Extracting pages: {[i + 1 for i in indices]}")

    # Assemble output
    sections = []
    for i, text in enumerate(pages, start=1):
        sections.append(f"\n\n{text}")
    full_text = "\n\n".join(sections)

    # Print or save
    if OUTPUT:
        Path(OUTPUT).write_text(full_text, encoding="utf-8")
        print(f"Text saved to '{OUTPUT}'")
    else:
        print("\n" + full_text)

    # Summary
    total_chars = sum(len(t) for t in pages)
    non_empty = sum(1 for t in pages if t.strip())
    print(f"\n--- Summary ---")
    print(f"Pages with text : {non_empty} / {len(pages)}")
    print(f"Total characters: {total_chars:,}")
    if non_empty == 0:
        print("\nWarning: no text was extracted. The PDF may be scanned/image-based.")
        print("For scanned PDFs, consider OCR tools such as pytesseract or ocrmypdf.")


if __name__ == "__main__":
    main()