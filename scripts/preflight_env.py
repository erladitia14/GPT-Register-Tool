#!/usr/bin/env python3
"""Pemeriksaan Lingkungan: Verifikasi ketersediaan Node.js, Playwright Chromium, dan paket Python kritis.

Ini adalah dependensi runtime yang TIDAK tercakup oleh `pip install -r requirements.txt` di bagian "Persyaratan Lingkungan" README:
extractor quickjs untuk Sentinel Token memerlukan `node`, dan inisialisasi Stripe untuk pembayaran protokol membutuhkan Playwright Chromium. Jika tidak ada, kerusakan runtime akan muncul sebagai OTP yang hilang diam-diam atau tautan pembayaran timeout, yang sulit dilacak.
Oleh karena itu, gunakan skrip ini untuk melakukan pemeriksaan satu kali sebelum peluncuran pertama.

Kode keluar: 0 berarti semua lulus; bukan 0 berarti ada item yang hilang (jumlah item gagal).
"""

from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path


def check_python_version() -> tuple[bool, str, str]:
    major, minor = sys.version_info[:2]
    if (major, minor) >= (3, 10):
        return True, f"Python {major}.{minor}", ""
    return False, f"Python {major}.{minor}", "Instal Python 3.10+ (https://www.python.org/downloads/)"


def check_node() -> tuple[bool, str, str]:
    exe = shutil.which("node")
    if not exe:
        return False, "node 不在 PATH", "安装 Node.js 18+（https://nodejs.org）并确保 node 在 PATH"
    try:
        proc = subprocess.run(
            [exe, "--version"], capture_output=True, text=True, timeout=10
        )
    except Exception as exc:  # pragma: no cover - defensive
        return False, f"Tidak dapat menjalankan node: {exc}", "Instal ulang Node.js 18+"
    version = (proc.stdout or proc.stderr or "").strip()
    return True, version or "node (versi tidak diketahui)", ""


def check_import(module: str, pip_name: str) -> tuple[bool, str, str]:
    try:
        __import__(module)
    except Exception as exc:
        return False, f"{module} tidak dapat diimpor: {exc}", f"python -m pip install {pip_name}"
    return True, module, ""


def check_playwright_chromium() -> tuple[bool, str, str]:
    try:
        from playwright.sync_api import sync_playwright
    except Exception as exc:
        return False, f"playwright tidak diinstal: {exc}", "python -m pip install playwright"
    try:
        with sync_playwright() as p:
            path = p.chromium.executable_path
    except Exception as exc:
        return False, f"Tidak dapat menanyakan chromium: {exc}", "python -m playwright install chromium"
    if path and Path(path).exists():
        return True, "chromium telah diinstal", ""
    return False, "chromium belum diunduh", "python -m playwright install chromium"


CHECKS = (
    ("Versi Python", check_python_version, True),
    ("Node.js (Sentinel quickjs)", check_node, True),
    ("Playwright Chromium (Stripe init)", check_playwright_chromium, True),
    ("curl_cffi (pembayaran protokol TLS)", lambda: check_import("curl_cffi", "curl_cffi"), True),
    ("requests", lambda: check_import("requests", "requests"), True),
    ("PyNaCl (Agent Identity Ed25519)", lambda: check_import("nacl", "PyNaCl"), False),
)


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")

    print("Pemeriksaan Pra-Lingkungan GPT-Register-Tool\n" + "=" * 40)
    failures: list[tuple[str, str, str]] = []
    for label, check, required in CHECKS:
        ok, detail, fix = check()
        tag = "  OK  " if ok else ("FAIL " if required else "WARN ")
        print(f"[{tag}] {label}: {detail}")
        if not ok and required:
            failures.append((label, detail, fix))

    print("=" * 40)
    if not failures:
        print("Semua dependensi kritis siap.")
        return 0

    print(f"Ditemukan {len(failures)} item hilang, cara perbaikan:")
    for label, _detail, fix in failures:
        print(f"  - {label}: {fix or 'lihat dokumen'}")
    return len(failures)


if __name__ == "__main__":
    raise SystemExit(main())
