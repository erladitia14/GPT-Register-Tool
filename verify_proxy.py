#!/usr/bin/env python3
"""Verifikasi apakah konfigurasi proxy berfungsi dengan benar.

Penggunaan:
  python verify_proxy.py
"""

import json
import sys

import requests


def test_proxy(proxy_url, test_url="https://ipinfo.io/json"):
    """Uji koneksi proxy dan lokasi geografis IP ekspor."""
    proxies = {"http": proxy_url, "https": proxy_url}
    try:
        r = requests.get(test_url, proxies=proxies, timeout=10)
        if r.status_code == 200:
            data = r.json()
            return {
                "ok": True,
                "ip": data.get("ip", ""),
                "country": data.get("country", ""),
                "city": data.get("city", ""),
                "org": data.get("org", ""),
            }
        return {"ok": False, "error": f"HTTP {r.status_code}"}
    except Exception as e:
        return {"ok": False, "error": str(e)}


def _load_config():
    try:
        with open("config.json", encoding="utf-8") as f:
            return json.load(f)
    except Exception:
        return {}


def main():
    cfg = _load_config()
    proxy_cfg = cfg.get("proxy") or {}
    default_proxy = proxy_cfg.get("default")
    pool = proxy_cfg.get("pool") or ([default_proxy] if default_proxy else [])
    paypal_cfg = cfg.get("paypal") or {}
    paypal_proxies = paypal_cfg.get("proxies") or []
    stage_proxies = paypal_cfg.get("stage_proxies") or {}

    print("=" * 60)
    print("Verifikasi konfigurasi proxy")
    print("=" * 60)

    # Uji proxy default
    print(f"\n[Proksi Default] {default_proxy or '(Tidak dikonfigurasi)'}")
    if default_proxy:
        result = test_proxy(default_proxy)
        if result["ok"]:
            print(f"  [OK] IP: {result['ip']}  Negara: {result['country']}  Kota: {result['city']}")
        else:
            print(f"  [FAIL] {result['error']}")
    else:
        print("  [SKIP] Tidak dikonfigurasi")

    # Uji kumpulan proksi
    if pool:
        print(f"\n[Pool Proxy] ({len(pool)} buah)")
        for i, proxy in enumerate(pool):
            result = test_proxy(proxy)
            status = f"OK IP={result['ip']} {result['country']}" if result["ok"] else f"FAIL {result['error']}"
            print(f"  [{i}] {proxy} -> {status}")

    # Uji proxy PayPal
    if paypal_proxies:
        print(f"\n[Proksi PayPal] ({len(paypal_proxies)} buah)")
        for i, proxy in enumerate(paypal_proxies):
            result = test_proxy(proxy)
            status = f"OK IP={result['ip']} {result['country']}" if result["ok"] else f"FAIL {result['error']}"
            print(f"  [{i}] {proxy} -> {status}")

    # Uji proxy bertahap PayPal
    if stage_proxies:
        print(f"\n[Proksi PayPal Bertahap]")
        for stage, proxy in stage_proxies.items():
            if proxy == "direct":
                print(f"  {stage}: direct (koneksi langsung)")
                continue
            result = test_proxy(proxy)
            status = f"OK IP={result['ip']} {result['country']}" if result["ok"] else f"FAIL {result['error']}"
            print(f"  {stage}: {proxy} -> {status}")

    # Uji koneksi langsung
    print(f"\n[Koneksi Langsung]")
    result = test_proxy(None)
    if result["ok"]:
        print(f"  [OK] IP: {result['ip']}  Negara: {result['country']}  Kota: {result['city']}")
    else:
        print(f"  [FAIL] {result['error']}")

    print("\n" + "=" * 60)
    print("Sumber Konfigurasi: config.json")
    print("=" * 60)


if __name__ == "__main__":
    main()
