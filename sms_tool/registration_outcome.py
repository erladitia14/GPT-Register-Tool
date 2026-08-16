"""
Modul penentuan hasil registrasi.

Dipisahkan dari registration.py, berisi:
- Normalisasi error tahap pembuatan akun (_create_account_error)
- Deteksi stabilitas AT (_probe_registration_access_token)
- Switch apakah alur registrasi bergantung refresh_token / kode verifikasi SMS (_requires_* dua fungsi kecil)

Fungsi-fungsi ini dipisah dari entrypoint utama register_loop, memudahkan testing terpisah atau penggunaan ulang.
"""

from collections.abc import Mapping

from .account_liveness import probe_account_liveness
from .config import CFG
from .registration_progress import registration_stage
import time


def _create_account_error(create_ok, create_data):
    """Menyaring kode kesalahan/pesan yang mudah dibaca manusia dari respons create_account."""
    if create_ok:
        return ""
    create_error = create_data.get("error") if isinstance(create_data.get("error"), dict) else {}
    create_code = str(create_error.get("code") or "").strip()
    create_message = str(create_error.get("message") or "").strip()
    error = "create_account_failed"
    if create_code:
        error += f":{create_code}"
    if create_message:
        error += f": {create_message}"
    return error


def _probe_registration_access_token(
    access_token,
    auth_session,
    proxy=None,
    *,
    cfg=None,
    probe_fn=None,
    stage_fn=None,
    sleep_fn=None,
):
    """
    Deteksi stabilitas AT multi-ronde.

    Probe access_token secara beruntun sebanyak count, semua probe harus 200 agar AT stabil;
    jika ada satu ronde yang bukan 200, langsung return, disertai vektor status_code tiap ronde.
    """
    runtime_cfg = cfg if isinstance(cfg, Mapping) else CFG
    registration_value = runtime_cfg.get("registration")
    registration_cfg = registration_value if isinstance(registration_value, Mapping) else {}
    probe_fn = probe_fn or probe_account_liveness
    stage_fn = stage_fn or registration_stage
    sleep_fn = sleep_fn or time.sleep
    try:
        timeout = max(5, min(int(registration_cfg.get("at_probe_timeout_seconds") or 30), 120))
    except (TypeError, ValueError):
        timeout = 30
    try:
        count = max(1, min(int(registration_cfg.get("at_stability_probe_count") or 2), 3))
    except (TypeError, ValueError):
        count = 2
    try:
        delay = max(0.0, min(float(registration_cfg.get("at_stability_probe_delay_seconds") or 10), 60.0))
    except (TypeError, ValueError):
        delay = 10.0
    probes = []
    for index in range(count):
        probe = probe_fn(
            {"access_token": access_token, "auth_session": auth_session or {}},
            proxy=proxy,
            timeout=timeout,
        )
        probes.append(probe)
        if int(probe.get("status_code") or 0) != 200:
            break
        if index + 1 < count and delay:
            stage_fn("access_token_stability_wait")
            sleep_fn(delay)
            stage_fn("access_token_probe")
    result = dict(probes[-1] if probes else {})
    result["stability_probe_count"] = len(probes)
    result["stability_status_codes"] = [int(item.get("status_code") or 0) for item in probes]
    result["stability_window_seconds"] = round(delay * max(0, len(probes) - 1), 3)
    return result


def _registration_requires_refresh_token(runtime_cfg=None):
    """Apakah alur pendaftaran perjanjian mengharuskan session hasil akhir harus mengandung refresh_token."""
    source = runtime_cfg if isinstance(runtime_cfg, Mapping) else CFG
    value = source.get("codex_oauth")
    cfg = value if isinstance(value, Mapping) else {}
    return bool(cfg.get("require_registration_refresh_token", True))


def _registration_requires_phone_verification(phone_pool=None, runtime_cfg=None):
    """Apakah alur pendaftaran perjanjian memerlukan verifikasi telepon sekunder (default: aktif jika ada phone_pool)."""
    source = runtime_cfg if isinstance(runtime_cfg, Mapping) else CFG
    value = source.get("codex_oauth")
    cfg = value if isinstance(value, Mapping) else {}
    default = bool(phone_pool)
    return bool(cfg.get("require_registration_phone_verification", default))
