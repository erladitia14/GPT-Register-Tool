namespace SmsWorkbench
{
    public sealed record SettingsSectionDefinition(string Title, IReadOnlyList<SettingDefinition> Fields);
    public sealed record SettingsCategoryDefinition(string Title, IReadOnlyList<SettingsSectionDefinition> Sections);

    public static class SettingsCatalog
    {
        private static SettingDefinition Text(string key, string label, string path, string fallback = "")
            => new(key, label, path, SettingFieldKind.Text, fallback);
        private static SettingDefinition Secret(string key, string label, string path)
            => new(key, label, path, SettingFieldKind.Secret);
        private static SettingDefinition Integer(string key, string label, string path, string fallback = "")
            => new(key, label, path, SettingFieldKind.Number, fallback);
        private static SettingDefinition Boolean(string key, string label, string path, bool fallback)
            => new(key, label, path, SettingFieldKind.Boolean, fallback ? "true" : "false");
        private static SettingDefinition Options(string key, string label, string path, string fallback, params string[] options)
            => new(key, label, path, SettingFieldKind.Options, fallback, options);
        private static SettingDefinition Multiline(string key, string label, string path, string fallback = "")
            => new(key, label, path, SettingFieldKind.Multiline, fallback);
        private static SettingsSectionDefinition Section(string title, params SettingDefinition[] fields) => new(title, fields);
        private static SettingsCategoryDefinition Category(string title, params SettingsSectionDefinition[] sections) => new(title, sections);

        public static IReadOnlyList<SettingsCategoryDefinition> Categories { get; } = new[]
        {
            Category("Email & Penerimaan",
                Section("Kolam Email",
                    Integer("otp_poll_interval", "Interval polling OTP (detik)", "email_registration.otp_poll_interval"),
                    Text("token_file", "File Kolam Email", "email_registration.token_file")),
                Section("ReMail",
                    Boolean("remail_enabled", "Aktifkan", "email_registration.remail.enabled", true),
                    Text("remail_base_url", "Alamat API", "email_registration.remail.base_url", "https://remail.aishop6.com"),
                    Secret("remail_api_key", "API Key", "email_registration.remail.api_key"),
                    Integer("remail_project_id", "ID Proyek", "email_registration.remail.project_id", "2"),
                    Integer("remail_product_id", "ID Produk", "email_registration.remail.product_id", "5"),
                    Options("remail_supply", "Strategi Stok", "email_registration.remail.supply", "private_first", "private_first", "public_only"),
                    Text("remail_email_suffix", "Sufiks Email", "email_registration.remail.email_suffix", "outlook.com")),
                Section("CFWorker",
                    Text("cfworker_url", "Worker URL", "email_registration.cfworker_url"),
                    Text("cfworker_domain", "Domain Email", "email_registration.cfworker_domain"),
                    Secret("cfworker_admin_token", "Admin Token", "email_registration.cfworker_admin_token"),
                    Secret("cfworker_api_token", "Cloudflare API Token", "email_registration.cfworker_api_token"))),

            Category("Registrasi & ambil kode",
                Section("SMSBower",
                    Secret("smsbower_api_key", "SMSBower API Key", "phone_reuse.smsbower.api_key"),
                    Integer("smsbower_sms_timeout", "Detik tunggu SMS", "phone_reuse.smsbower.sms_timeout"),
                    Integer("smsbower_sms_poll_interval", "Detik interval polling SMS", "phone_reuse.smsbower.sms_poll_interval"),
                    Integer("phone_max_reuse_count", "Jumlah Penggunaan Ulang", "phone_reuse.max_reuse_count"),
                    Integer("phone_send_cooldown_seconds", "Detik Pending Pengiriman Kode", "phone_reuse.send_cooldown_seconds"),
                    Integer("phone_send_retry_attempts", "Jumlah Percobaan Ulang Kirim Kode", "phone_reuse.send_retry_attempts"),
                    Integer("phone_send_retry_delay_seconds", "Detik Tunda Ulang Kirim Kode", "phone_reuse.send_retry_delay_seconds"),
                    Text("phone_state_file", "File status", "phone_reuse.state_file")),
                Section("Codex OAuth",
                    Integer("codex_registration_timeout", "Waktu habis OAuth (detik)", "codex_oauth.registration_timeout"),
                    Boolean("codex_allow_passwordless_takeover", "Izinkan OTP email sebagai cadangan", "codex_oauth.allow_passwordless_takeover", false),
                    Boolean("codex_auto_phone_verification", "Verifikasi otomatis via SMS", "codex_oauth.auto_phone_verification", false),
                    Boolean("codex_require_registration_refresh_token", "Registrasi membutuhkan RT", "codex_oauth.require_registration_refresh_token", true),
                    Boolean("codex_require_registration_phone_verification", "Registrasi membutuhkan nomor ponsel", "codex_oauth.require_registration_phone_verification", true)),
                Section("AT Disimpan dengan Stabil",
                    Integer("registration_at_stability_probe_count", "AT Jumlah Probe", "registration.at_stability_probe_count", "2"),
                    Integer("registration_at_stability_probe_delay", "Interval deteksi (detik)", "registration.at_stability_probe_delay_seconds", "10"),
                    Integer("registration_at_probe_timeout", "Detik Waktu Habis Deteksi Tunggal", "registration.at_probe_timeout_seconds", "30")),
                Section("Konkurensi Tahap",
                    Integer("registration_network_concurrency", "Konkurensi jaringan registrasi", "registration.stage_concurrency.network", "4"),
                    Integer("registration_at_probe_concurrency", "AT Konkurensi Probe", "registration.stage_concurrency.at_probe", "4")),
                Section("Sentinel",
                    Text("sentinel_version", "Versi Sentinel", "email_registration.sentinel_version"),
                    Integer("sentinel_max_concurrency", "Konkurensi ekstraksi", "email_registration.sentinel_max_concurrency", "2"),
                    Integer("sentinel_prewarm_window", "Jendela pemanasan satu lawan satu", "email_registration.sentinel_prewarm_window", "4"),
                    Integer("sentinel_circuit_failures", "Jumlah kegagalan pemutusan", "email_registration.sentinel_circuit_failures", "3"),
                    Integer("sentinel_circuit_cooldown", "Detik cooldown pemutusan", "email_registration.sentinel_circuit_cooldown_seconds", "60"))),

            Category("Impor & Akun",
                Section("CPA",
                    Text("cpa_api_url", "Alamat CPA", "cpa_mode.api_url"),
                    Secret("cpa_api_token", "CPA Token", "cpa_mode.api_token")),
                Section("SUB2API",
                    Text("sub2api_url", "Alamat API", "sub2api.api_url"),
                    Secret("sub2api_token", "API Token", "sub2api.api_token"),
                    Text("sub2api_email", "Email masuk", "sub2api.email"),
                    Secret("sub2api_password", "Kata sandi masuk", "sub2api.password"),
                    Text("sub2api_group", "Target grup", "sub2api.group_name"),
                    Text("sub2api_group_ids", "ID Grup", "sub2api.group_ids"),
                    Text("sub2api_proxy", "Proxy Jarak Jauh", "sub2api.proxy_name"),
                    Text("sub2api_proxy_id", "ID Proxy", "sub2api.proxy_id"),
                    Integer("sub2api_priority", "Prioritas", "sub2api.priority"),
                    Integer("sub2api_concurrency", "Konkurensi Akun", "sub2api.concurrency"),
                    Options("sub2api_auth_mode", "Mode Kredensial", "sub2api.auth_mode", "auto", "auto", "oauth", "agent_identity"),
                    Boolean("sub2api_verify_after_import", "Uji Konektivitas Setelah Impor", "sub2api.verify_after_import", true))),

            Category("Jaringan & Pembayaran",
                Section("Jaringan Dasar",
                    Text("registration_proxy", "Proxy registrasi (utama)", "", "http://127.0.0.1:7897"),
                    Multiline("registration_proxy_pool", "Pool proxy registrasi", ""),
                    Text("mailbox_proxy", "Proxy Penerima Email", "", "http://127.0.0.1:7897")),
                Section("Manajemen Protokol",
                    Multiline("protocol_proxy_pool", "Kolam Agen Pembayaran Perjanjian", ""),
                    Text("protocol_enabled_methods", "Metode Aktivasi", "", "paypal,gopay,gcash,grabpay,upi,ideal,pix,kakao,blik,twint,direct_card,momo"),
                    Text("protocol_reference_root", "Direktori ekstraktor", "protocol_payments.reference_root", "services/protocol-payment"),
                    Text("protocol_state_file", "File status", "protocol_payments.state_file", "runtime/payment_link_runs.jsonl"),
                    Integer("protocol_timeout_seconds", "Detik Waktu Habis Protokol", "protocol_payments.timeout_seconds", "900")),
                Section("Pembayaran batch formal",
                    Integer("protocol_batch_momo_workers", "Concurrency MoMo", "protocol_payments.batch.method_workers.momo", "2"),
                    Integer("protocol_batch_kakao_workers", "Concurrency Kakao", "protocol_payments.batch.method_workers.kakao", "2"),
                    Integer("protocol_batch_gopay_workers", "Concurrency GoPay", "protocol_payments.batch.method_workers.gopay", "2"),
                    Integer("protocol_batch_gcash_workers", "Concurrency GCash", "protocol_payments.batch.method_workers.gcash", "2"),
                    Integer("protocol_batch_grabpay_workers", "Concurrency GrabPay", "protocol_payments.batch.method_workers.grabpay", "2"),
                    Boolean("protocol_batch_pause_on_canary_failure", "Canary gagal dijeda", "protocol_payments.batch.pause_on_canary_failure", true),
                    Integer("protocol_batch_canary_pause_seconds", "Jeda (detik)", "protocol_payments.batch.canary_pause_seconds", "21600"),
                    Multiline("protocol_payment_matrix", "JSON Matriks Kelayakan Wilayah", "")),
                Section("PayPal",
                    Text("paypal_proxy", "Proxy PayPal", ""),
                    Options("paypal_billing_region", "Wilayah pembuatan pesanan", "", "DE", "JP", "US", "AU", "DE", "FR", "GB", "IN", "BR"),
                    Options("paypal_link_generation_type", "Mode pembuatan tautan langsung PayPal", "paypal.link_generation_type", "hosted_long_url", "hosted_long_url", "paypal_direct", "paypal_direct_zero_due")),
                Section("Agen Pembayaran Wilayah",
                    Text("protocol_gopay_proxy", "GoPay", "protocol_payments.methods.gopay.proxy"),
                    Text("protocol_gcash_proxy", "GCash", "protocol_payments.methods.gcash.proxy"),
                    Text("protocol_grabpay_proxy", "GrabPay", "protocol_payments.methods.grabpay.proxy"),
                    Text("protocol_ideal_proxy", "iDEAL", "protocol_payments.methods.ideal.proxy"),
                    Text("protocol_pix_proxy", "PIX", "protocol_payments.methods.pix.proxy"),
                    Text("protocol_kakao_proxy", "Kakao Pay", "protocol_payments.methods.kakao.proxy"),
                    Text("protocol_blik_proxy", "BLIK", "protocol_payments.methods.blik.proxy"),
                    Text("protocol_twint_proxy", "TWINT", "protocol_payments.methods.twint.proxy"),
                    Text("protocol_direct_card_proxy", "Direct Card Checkout", "protocol_payments.methods.direct_card.proxy"),
                    Text("protocol_momo_proxy", "MoMo", "protocol_payments.methods.momo.proxy"))),

            Category("Data & file",
                Section("Penyimpanan lokal",
                    Text("output_directory", "Direktori Session", "output.directory"),
                    Text("sqlite_path", "Jalur SQLite", "storage.sqlite_path")))
        };

        public static IEnumerable<SettingDefinition> AllFields => Categories.SelectMany(category => category.Sections).SelectMany(section => section.Fields);
    }
}
