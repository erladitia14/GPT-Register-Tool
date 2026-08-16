# GPT-Register-Tool

Tool untuk Windows: pendaftaran akun ChatGPT, OTP email, manajemen akun, ekstraksi link pembayaran berbasis protokol, dan eksekusi pembayaran eksplisit.

Proyek ini menggunakan **desktop WPF + inti bisnis Python**: sisi desktop menangani pintu masuk operasi, konfigurasi, dan tampilan hasil, sedangkan modul Python menangani email, pendaftaran, sesi, pembayaran, proxy, dan protokol layanan eksternal. Data operasional disimpan secara default di mesin lokal dan tidak ditulis ke Git.

## Penjelasan Proyek

### Alur Utama

```text
Sumber email
  -> Pendaftaran OTP email ChatGPT
  -> Ambil Access Token / Session, dengan AT HTTP 200 yang stabil sebagai batas masuk database
  -> Opsional: verifikasi nomor HP dan Codex OAuth
  -> Deteksi/refresh JIT AT serta ekstraksi opsional link pembayaran protokol
  -> Session JSON + indeks SQLite
  -> Manajemen terpadu via desktop WPF
```

### Skenario Penggunaan

- Melakukan pendaftaran email massal dari pool email, ReMail, atau CFWorker.
- Polling OTP terpadu untuk email Microsoft, Gmail, iCloud link penerima kode, ReMail, CFWorker, dll.
- Mengelola akun lokal, Session, status kuota, dan link pembayaran.
- Memilih exit proxy berdasarkan fase dan mengekstrak link PayPal atau metode pembayaran lokal lainnya.
- Mengekspor data akun ke format target seperti Codex, CPA, SUB2API.

### Stack Teknologi

| Layer | Teknologi |
| --- | --- |
| Desktop | WPF, .NET 10, C#, Generic Host, CommunityToolkit.Mvvm, WPF-UI |
| Inti bisnis | Python 3, curl_cffi, requests, httpx, PyNaCl (Ed25519) |
| Penyimpanan data | JSON, JSONL, SQLite |
| Protokol email | ReMail API, CFWorker, iCloud link penerima kode, Microsoft Graph/OAuth, IMAP, Gmail IMAP |
| Protokol pembayaran | Stripe Checkout, PayPal, GoPay, GCash, GrabPay, UPI, iDEAL, PIX, Kakao Pay, BLIK, TWINT, Kartu Langsung Checkout, MoMo |
| Bantuan browser | Playwright, Camoufox, CloakBrowser |

## Cara Instalasi & Deployment

### Persyaratan Lingkungan

- Windows 10/11 x64.
- Python 3.10 atau lebih baru.
- `curl_cffi==0.16.0`. Pra-pemeriksaan pendaftaran akan memvalidasi versi yang terpasang dan profile `chrome146`; versi lama tidak akan masuk ke tahap pengadaan email atau pendaftaran.
- .NET 10 Desktop Runtime; untuk compile dari source code diperlukan .NET 10 SDK.
- **Node.js 18+** (`node` harus ada di PATH): extractor quickjs untuk Sentinel Token menjalankan `sdk.js` asli OpenAI menggunakan `node`; jika tidak ada, OTP akan hilang secara diam-diam pada tahap pendaftaran.
- **Playwright Chromium**: Stripe init untuk pembayaran protokol MoMo/kartu langsung menggunakan network stack Chromium untuk menyelesaikan TLS, jalankan `python -m playwright install chromium`.
- Lingkungan jaringan yang bisa mengakses email target, ChatGPT, dan layanan pembayaran secara normal.
- Proxy pendaftaran, proxy penerima email, dan proxy pembayaran protokol saling independen; penerimaan email secara default menggunakan lokal `http://127.0.0.1:7897`.

Setelah menginstal dependensi, jalankan pra-pemeriksaan lingkungan untuk memastikan Node.js, Playwright Chromium, dan paket Python utama sudah siap:

```powershell
python scripts/preflight_env.py
```

### Cara 1: Installer

Unduh yang terbaru dari GitHub Releases:

```text
GPT-Register-Tool-Setup-vYYYY.MM.DD.exe
```

Jalankan installer dan pilih direktori instalasi. Sebelum menjalankan pertama kali, tetap perlu menginstal dependensi Python dan membuat konfigurasi lokal:

```powershell
python -m pip install -r requirements.txt
copy config.example.json config.json
```

### Cara 2: ZIP Portabel

Unduh dan ekstrak:

```text
GPT-Register-Tool-win-x64-vYYYY.MM.DD.zip
```

Jalankan di direktori hasil ekstraksi:

```powershell
python -m pip install -r requirements.txt
copy config.example.json config.json
.\dist\net10\SmsWorkbench.exe
```

### Cara 3: Jalankan dari Source Code

```powershell
git clone https://github.com/2951461586/GPT-Register-Tool.git
cd GPT-Register-Tool
python -m pip install -r requirements.txt
copy config.example.json config.json
powershell -ExecutionPolicy Bypass -File .\SmsWorkbench\build_dotnet.ps1
.\dist\net10\SmsWorkbench.exe
```

Program desktop hanya bisa di-compile lewat `SmsWorkbench/build_dotnet.ps1`. Jangan langsung menjalankan `dotnet build`, karena itu hanya menghasilkan file intermediate dan tidak akan memperbarui workspace standar `dist/net10`.

### Konfigurasi Pertama Kali

Buka halaman **Pengaturan** di desktop, minimal selesaikan konfigurasi berikut:

1. Di **Jaringan & Pembayaran**, konfigurasikan masing-masing pool proxy pendaftaran, proxy penerima email, dan pool proxy pembayaran protokol.
2. Di **Email & Penerimaan**, konfigurasikan ReMail, CFWorker, atau sumber email lainnya.
3. Sesuai kebutuhan, konfigurasikan SMSBower, CPA, SUB2API, dan parameter pembayaran protokol masing-masing.
4. Setelah disimpan, buka kembali fitur terkait untuk menggunakan konfigurasi baru.

ReMail API Key juga bisa diberikan lewat environment variable:

```powershell
$env:REMAIL_API_KEY = "rk-your-key"
```

Environment variable diprioritaskan di atas `config.json`. API Key yang disimpan dari halaman pengaturan desktop hanya ditulis ke `config.json` lokal yang diabaikan oleh Git.

## Fitur Unggulan Proyek

### Pendaftaran Satu Klik

- Mendukung pendaftaran via pool email, ReMail penerima kode jangka pendek, email domain CFWorker, dan nomor HP SMSBower.
- Mendukung pendaftaran akun tunggal maupun batch paralel.
- Setiap akun yang terdaftar mengekstrak Sentinel Token dan `oai-did` secara independen, tidak menggunakan ulang transaksi autentikasi lintas akun; `_extract_sentinel` secara default mengizinkan ekstraksi paralel 2 jalur (`sentinel_max_concurrency`, maksimal 4), menyeimbangkan kecepatan batch dengan risiko rate limit Sentinel.
- Alur pendaftaran hanya bertanggung jawab autentikasi akun dan menyimpan AT/Session, tidak lagi menghasilkan link pembayaran.
- Penentuan keberhasilan pendaftaran berdasarkan deteksi AT HTTP 200; kandidat yang tidak terus-menerus mengembalikan 200 dalam jendela deteksi stabil tidak akan masuk ke database akun aktif.
- Alur pendaftaran tidak lagi menjalankan tahap Agent Identity; jika Agent Identity dibutuhkan, harus diproses lewat jalur impor SUB2API eksplisit.
- Saat record email dipilih, pendaftaran memprioritaskan email yang dipilih; saat tidak ada email yang dipilih, akan ditampilkan pemilih sumber email.
- Pendaftaran, OTP, pengambilan Session, dan Codex OAuth masing-masing mencatat hasil per fase, menghindari salah melaporkan status menengah sebagai sukses; ekstraksi link pembayaran hanya dipicu oleh operasi pembayaran independen.

### Konsistensi Protokol dan Recovery

- CLI secara berurutan melakukan pra-pemeriksaan 3 tahap jaringan (ChatGPT, Auth, Sentinel) sebelum mengambil atau membeli email, dan memilih jalur tersedia pertama dari pool proxy pendaftaran; jika TLS, proxy, atau profile tidak memenuhi persyaratan, email tidak akan terus dikonsumsi.
- Setiap akun terikat pada session proxy independen; pendaftaran batch hanya mengganti proxy dan membuat session baru saat terjadi kegagalan jaringan atau status autentikasi, tidak memecah transaksi yang sama ke exit yang berbeda.
- NextAuth, Auth API, dan ChatGPT menggunakan template Header masing-masing, dan berbagi `oai-did`, `oai-session-id`, ID pemanggilan, UA, dan client hints yang stabil.
- Bahasa dan zona waktu Fingerprint dibuat berdasarkan GeoIP proxy; Sentinel QuickJS menggunakan UA, platform, zona waktu, layar, memori, dan client hints yang sama.
- Sentinel masing-masing menghasilkan token `username_password_create`, `authorize_continue`, dan `oauth_create_account`. Jika DID di Token, Cookie, dan Header tidak konsisten, akan diakhiri dengan `sentinel_extract_failed`.
- Setelah session tunggal menerima 403/429, circuit breaker dibuka dan tidak melanjutkan request selama masa cooldown; pendaftaran produksi tidak diizinkan melakukan downgrade ke pure HTTP PoW.
- Setelah akun dibuat dan AT diperoleh, kandidat dan breakpoint langsung dipersistenkan, lalu baru menjalankan deteksi AT HTTP 200. Kegagalan deteksi proxy/TLS bisa di-recover dari breakpoint tanpa mengulang OTP email dan pembuatan akun.

### Sumber Email ReMail

- Sumber pendaftaran satu klik menyediakan `ReMail email jangka panjang`, seragam menggunakan mode email jangka panjang `purchase`.
- Mendukung pembuatan order email satuan maupun batch.
- Order massal seratus akun secara default memperpanjang waktu tunggu HTTP 2 detik per email (minimal 30 detik), bisa di-override via `email_registration.remail.batch_timeout`.
- Mendukung strategi stok `private_first`, `public_only`.
- Mendukung penentuan proyek, produk, dan suffix email.
- Menggunakan `Idempotency-Key` untuk mencegah order duplikat akibat retry.
- Pembuatan order menggunakan API Key; penerimaan email menggunakan alamat email dan Service Token independen.
- Jika Service Token mengembalikan 401, akan menggunakan API Key untuk mencari order miliknya; jika server mengembalikan Token baru, akan disimpan ke Session JSON dan SQLite lalu retry sekali.
- Order `code` hanya bisa menerima email sebelum `receiveUntil`, API Key tidak bisa menggantikan Service Token yang kedaluwarsa; jika perlu terus memantau inbox setelahnya, pilihlah `purchase`.
- Jika ringkasan email tidak ada kode verifikasi, otomatis membaca detail email dan menjalankan filter waktu, penerima, message ID, dan kode verifikasi yang sudah dikecualikan.
- Jika ReMail mengembalikan kode verifikasi 6 digit terstruktur dari pengirim OpenAI yang terpercaya, akan memvalidasi penerima presisi dan timestamp; meskipun subjek ter-lokalisasi mengalami garbled, tidak akan menunggu timeout secara salah.
- Desktop bisa membuka inbox dari record pendaftaran ReMail; mode lihat membaca isi lengkap email dan kode verifikasi.
- Log akan melakukan mask (desensitisasi) terhadap API Key dan Service Token.
- Polling OTP adaptif: delay awal 1s, backoff progresif (1s → 1.5s → 3s), menyesuaikan interval polling secara dinamis berdasarkan status kedatangan email dan saran rate limit server, mengurangi request yang tidak berguna.
- Waktu penerimaan ReMail mengizinkan deviasi clock server default 90s; snapshot message ID tetap mencegah kode verifikasi lama digunakan berulang kali.
- Jika ReMail belum menerima kode verifikasi dalam 30 detik, akan melakukan resend sekali, sisa waktu terus menerima kode verifikasi terbaru dalam transaksi ini.
- Order ReMail yang sudah ada bisa ditulis ke file token email dalam format `remail://email---serviceToken---orderNo---purchaseId` untuk digunakan kembali tanpa perlu membeli ulang.
- Saat pembelian massal menemui timeout atau 5xx yang bisa di-retry, akan terlebih dahulu mencocokkan order baru secara ketat berdasarkan jendela waktu request, proyek, produk, dan kuantitas; hanya saat cocok persis akan di-recover otomatis, menghindari pembelian duplikat setelah respons hilang.
- `ReMail email jangka panjang` akan menambah AT HTTP 200 yang stabil sesuai jumlah pendaftaran, desktop menggunakan batas pengadaan default dan mengelola batch pendaftaran secara otomatis; mode ini mengaktifkan verifikasi HP SMSBower secara default. CLI tetap bisa menyetel batasan tambahan via `--max-mailbox-purchases` dan `--max-remail-cost`.

### Email Terpadu dan OTP

Seam mailbox terpadu mendukung:

- ReMail.
- Email domain CFWorker.
- Microsoft Graph/OAuth.
- Fallback IMAP Outlook/Hotmail.
- Gmail IMAP dan SMTP.
- iCloud link penerima kode, baik "impor email" desktop maupun backend mendukung format `email----URL-penerima-kode` dan `email---URL-penerima-kode`.
- Chatai, file token, dan format pool email historis.

Parsing OTP mendukung pencocokan subjek, filter pengirim, pencocokan presisi penerima, filter timestamp server, dan pemeringkatan kandidat.

### Ekstraksi Link Pembayaran Protokol

- Mendukung PayPal, GoPay, GCash, GrabPay, UPI, iDEAL, PIX, Kakao Pay, BLIK, TWINT, Kartu Langsung Checkout, MoMo.
- BLIK mengirim kode 6 digit sekali pakai dan langsung mengeksekusi pembayaran, hanya tersedia di popup/perintah pembayaran protokol akun tunggal, tidak masuk ke ekstraksi link otomatis pasca-pendaftaran atau pemilih pembayaran batch.
- Kartu Langsung Checkout (Filipina PH/PHP): order US → apply promo TR → validasi 0 yuan, menghasilkan link panjang checkout kartu langsung `chatgpt.com/checkout/<entity>/<cs_id>`.
- MoMo (Vietnam VN/VND): order → Stripe init → paksa ₫0 → buat PM MoMo → Confirm → Approve → ikuti redirect, menghasilkan QR code `payment.momo.vn` yang bisa di-scan (otomatis di-decode menjadi PNG untuk "buka QR code").
- GoPay (Indonesia ID/IDR), GCash dan GrabPay (Filipina PH/PHP) menggunakan adapter wallet yang sama: Checkout → Stripe init → buat PM wallet → Confirm → Approve → Poll → validasi Provider Redirect. Ketiganya hanya dibedakan berdasarkan wilayah, mata uang, locale, dan whitelist domain akhir.
- PayPal mendukung link panjang Hosted, direct link PP, dan mode trial paksa 0 yuan.
- Rekonsiliasi redirect PayPal ditangani oleh `paypal_reconciliation.py` yang independen, hanya melacak Stripe Return → OpenAI Pay → Checkout Verify dalam whitelist, dan mengeluarkan bukti `conclusive`/`unknown`/`failed` yang sudah di-mask; tidak mengubah interface ekstraksi link, juga tidak menghasilkan atau meng-overwrite link pembayaran.
- Mendukung proxy per segmen `checkout`, `approve`, `update`.
- Proxy dinamis akan otomatis menulis ulang negara dan Session sesuai metode pembayaran, mendukung exit target seperti US, JP, VN, ID, IN, NL, BR, KR, PL, CH, PH.
- Pool proxy pembayaran protokol dideteksi berurutan, otomatis berpindah ke proxy berikutnya jika proxy saat ini tidak tersedia atau negara exit tidak cocok.
- Pemilihan wilayah dan proxy disimpan sebagai riwayat.
- Mendukung pengujian aktual terhadap IP exit proxy, negara, dan kecocokan wilayah yang diharapkan.
- Membedakan secara ketat tahap Checkout, pembuatan PM, Confirm, Poll pertama, hingga Provider Redirect final.
- Status akhir umum ekstraksi link adalah `completed`, `failed`, `cancelled`, `unknown`, `timed_out`, setiap hasil memiliki `retryable` dan `error_stage`. `unknown` akan ditandai tambahan `requires_reconciliation=true`, retry otomatis dilarang sebelum rekonsiliasi selesai; `cancelled` tidak di-retry, `timed_out` biasa bisa di-retry sesuai kebijakan.
- Ekstraksi link batch sebaiknya terlebih dahulu memfilter akun non-401 via interface kuota lokal, baru menjalankan protokol pembayaran; laporan harus menghitung secara terpisah: AT tersedia, kelayakan paket/trial, visibilitas metode pembayaran, keberhasilan Approve, dan produk akhir link/QR code.
- MoMo hanya dianggap sukses jika mengembalikan `ready_with_qr` dan menghasilkan URL `payment.momo.vn` atau file QR code; `account_trial_ineligible`, `card_only_full_price`, dan `approve_result_blocked` semuanya adalah status gagal yang jelas.

- Eksekutor pembayaran batch mendukung JIT AT, recovery bertingkat HTTP 401 (RT, Cookie, OTP email browser terisolasi, Codex OAuth), deteksi kelayakan, jeda Canary, paralelisasi level metode, retry transien, breakpoint atomik, dan lanjut batch yang sama.
- MoMo menggunakan proxy per tahap sesuai Checkout, Promotion, Stripe Provider, Approve, Redirect; Kakao mengeluarkan hasil terstruktur, hanya Redirect Kakao/Nicepay yang jelas yang dianggap link sukses.

### Agent Identity dan Batas Impor SUB2API

- Alur utama pendaftaran sudah menghapus tahap Agent Identity/task, kegagalan Agent Identity tidak akan mengubah hasil pendaftaran AT 200.
- Agent Identity JSON yang sudah ada tetap bisa dibaca oleh jalur impor SUB2API eksplisit; pembuatan baru/rebuild juga hanya bisa dipicu lewat alur impor tersebut.
- Agent Identity menggunakan private key Ed25519 PKCS#8, disimpan independen di `sessions/agent_identities/`, private key tidak ditulis ke log.
- Mendukung `--register-and-import` untuk otomatis impor SUB2API setelah pendaftaran selesai.
- Impor SUB2API mendukung 3 mode kredensial: `auto`, `oauth`, `agent_identity`; hanya memengaruhi batas impor, tidak akan menyisipkan kembali tahap pendaftaran.
- Format ekspor SUB2API kompatibel dengan backend Go, field `expires_at` menggunakan timestamp Unix (int64).
- Bisa melewati validasi konektivitas pasca-impor via `--sub2api-no-verify`.

### Manajemen Akun dan Data

- Indeks ganda Session JSON dan SQLite.
- Status akun, AT (diperoleh/belum diperoleh/invalid 401), RT, link pembayaran, dan hasil verifikasi nomor HP ditampilkan terpusat.
- "Pengujian Akun" di sidebar kiri bertanggung jawab atas pemeriksaan kesehatan AT/kuota; HTTP 401 akan mencoba RT, Cookie, OTP email browser terisolasi, dan Codex OAuth secara berurutan di dalam recovery eksplisit atau alur JIT pembayaran.
- Mendukung salin AT, lihat email, daftar ulang, dan regenerate link pembayaran.
- Mendukung alur impor/ekspor Codex JSON, CPA, SUB2API, dll.
- Daftar akun mempertahankan wilayah pendaftaran, batch pendaftaran, dan status masuk database, memudahkan memilih akun pembayaran batch berdasarkan cohort.
- Data lokal secara default disimpan di `sessions/` dan `runtime/`, keduanya diabaikan oleh Git.

### Operasi Pembayaran Batch Desktop

1. Centang akun yang akan diproses di daftar akun, buka "Pembayaran Protokol Batch" di sidebar kiri atau menu klik kanan dengan nama yang sama.
2. Pilih metode ekstraksi link seperti MoMo, Kakao, Kartu Langsung Checkout, atur paralelisasi, retry transien, jumlah Canary, batch ID, dan proxy Seed.
3. Secara default aktifkan "Recovery Otomatis 401"; jika dicentang "Hanya Deteksi Kelayakan", akan menyelesaikan JIT AT, matriks wilayah pendaftaran, ChatGPT Checkout, dan Stripe init, lalu berhenti sebelum pembuatan PM, Confirm, Approve, dan Provider Redirect. Hasil akan mencatat secara jelas jumlah, mata uang, visibilitas metode pembayaran, dan klasifikasi `eligible`/`ineligible`/`unknown`.
4. Konfirmasi kombinasi wilayah Register Region, Checkout, Promotion, Provider, Approve, dan Redirect melalui "Matriks Wilayah Akun / Kelayakan Pembayaran".
5. Dengan parameter mode, matriks, proxy, dan retry yang sama, penggunaan batch ID yang sama secara berulang bisa membaca breakpoint atomik dari `runtime/payment_batches/` dan melanjutkan eksekusi; saat parameter berubah, ketidakcocokan signature akan menjalankan ulang, hasil deteksi tidak akan digunakan ulang untuk pembayaran resmi. Canary `unknown` sistematis akan menjeda seluruh batch lanjutan metode tersebut, ketidakteradaan metode pembayaran atau penawaran non-nol yang jelas tidak akan disalahartikan sebagai kegagalan protokol. Laporan akan menampilkan secara terpisah jumlah AT 200, refresh JIT, deteksi kapabilitas, kelayakan, link, QR code, dan kegagalan.

### Penerima Kode HP

- Mendukung query negara dan tier harga SMSBower.
- Mendukung konfigurasi retry pengiriman, timeout tunggu, dan interval polling.
- Mendukung verifikasi HP Codex OAuth dan alur refresh akun.
- Operasi batch mempertahankan pemetaan hasil email dan nomor HP, memudahkan investigasi kegagalan akun tunggal.

## Arsitektur Proyek

### Struktur Berlapis

```text
SmsWorkbench/
  Desktop WPF
  -> Generic Host / DI composition root
  -> Halaman MVVM progresif, konfigurasi, daftar, peluncuran task, tampilan status

IBackendClient
  -> ArgumentList + pembatalan/timeout/terminasi process tree
  -> Envelope hasil versioned satu baris @@SMSWORKBENCH_IPC_V1@@

sms_tool/cli.py
  CLI dan orkestrasi task
  -> Parsing parameter, task batch, status exit proses

sms_tool/registration.py
  Alur utama pendaftaran
  -> OTP email, pembuatan akun, Session, Codex OAuth

sms_tool/registration_concurrency.py
  Gate resource tahap pendaftaran
  -> Batas paralelisasi jaringan, deteksi AT, dan tahap pembayaran serta metrik tunggu

sms_tool/account_liveness.py / account_recovery.py
  Kelangsungan hidup dan recovery akun
  -> Deteksi kuota tanpa efek samping, recovery OAuth eksplisit, dan persistensi status

sms_tool/payment_auth.py / payment_batch.py
  Gate JIT AT dan pembayaran protokol batch
  -> Recovery bertingkat 401, deteksi kapabilitas Checkout/Stripe, matriks kelayakan, Canary, retry, laporan breakpoint

sms_tool/checkout_contract.py / payment_capability.py
  Kontrak Checkout terpadu dan deteksi kapabilitas metode pembayaran
  -> Wilayah/mata uang/locale, Stripe init, normalisasi katalog jumlah dan metode pembayaran

sms_tool/wallet_provider.py / wallet_transport.py
  Adapter wallet bersama GoPay, GCash, GrabPay
  -> PM, Confirm, Approve, Poll, Provider Redirect, dan proxy per tahap

sms_tool/mailbox.py
  Routing terpadu email
  -> ReMail / CFWorker / Graph / IMAP / Gmail

sms_tool/payment_link_manager.py
  Manajer pembayaran protokol
  -> Registrasi metode, proxy per segmen, lima status akhir, hasil terpadu retryable/error_stage

sms_tool/paypal_reconciliation.py
  Rekonsiliasi redirect PayPal independen
  -> State machine lompatan whitelist, desensitisasi secret, klasifikasi kesimpulan/unknown

sms_tool/storage.py
  Persistensi data
  -> Session JSON, SQLite, status, dan deduplikasi

services/
  Layanan protokol lokal opsional
  -> Diagnosa email, extractor pembayaran lainnya
```

### Modul Inti

| Modul | Tanggung Jawab |
| --- | --- |
| `SmsWorkbench/` | Antarmuka desktop WPF, halaman pengaturan, pintu masuk task, dan tampilan status lokal |
| `sms_tool/cli.py` | Parameter CLI dan orkestrasi task tingkat tinggi |
| `sms_tool/registration.py` | Pendaftaran ChatGPT, OTP, Session, dan verifikasi lanjutan |
| `sms_tool/registration_concurrency.py` | Grup resource tahap pendaftaran, gate paralelisasi, dan metrik tunggu |
| `sms_tool/account_liveness.py` | Deteksi kelangsungan hidup `/backend-api/wham/usage`, klasifikasi respons, dan parsing kuota |
| `sms_tool/account_recovery.py` | Refresh kuota lokal, recovery bertingkat 401, validasi AT kandidat, dan persistensi akun dinonaktifkan |
| `sms_tool/mailbox.py` | Routing provider email dan polling OTP terpadu |
| `sms_tool/mailbox_remail.py` | Order, penerimaan email, pembacaan detail, dan ekstraksi OTP ReMail |
| `sms_tool/mailbox_cfworker.py` | Pembuatan email dan penerimaan CFWorker |
| `sms_tool/mailbox_graph.py` | Microsoft OAuth dan batas Graph |
| `sms_tool/mailbox_gmail.py` | Gmail IMAP/SMTP dan OAuth |
| `sms_tool/mailbox_icloud_url.py` | Penerimaan iCloud link penerima kode, parsing body HTML/API, dan normalisasi OTP |
| `sms_tool/payment_link_manager.py` | Registrasi metode pembayaran, state machine, dan hasil terpadu |
| `sms_tool/checkout_contract.py` | ChatGPT Checkout, request/respons Stripe init, dan kontrak bukti kapabilitas metode pembayaran |
| `sms_tool/payment_capability.py` | Deteksi kapabilitas umum hanya sampai Checkout + Stripe init |
| `sms_tool/wallet_provider.py` | Orkestrasi bersama dan hasil terstruktur GoPay, GCash, GrabPay |
| `sms_tool/wallet_transport.py` | HTTP riil wallet, proxy per tahap, dan validasi Provider Redirect |
| `sms_tool/gen_pp_link.py` | PayPal/Stripe Checkout dan pembuatan link |
| `sms_tool/paypal_proxy.py` | Proxy per segmen, rotasi wilayah, dan deteksi exit |
| `sms_tool/paypal_reconciliation.py` | Rekonsiliasi redirect merchant PayPal independen dari ekstraksi link dan bukti ter-mask |
| `sms_tool/storage.py` | SQLite, indeks Session, dan persistensi status |
| `sms_tool/agent_identity.py` | Konversi kredensial Agent Identity SUB2API eksplisit, pembuatan kunci Ed25519, dan persistensi |
| `sms_tool/sub2api_import.py` | Impor SUB2API (multi mode autentikasi) |
| `sms_tool/session_converter.py` | Konversi akun dan Session multi-format |
| `sms_tool/payment_auth.py` | Deteksi AT sebelum pembayaran, recovery bertingkat 401, dan telemetri aman |
| `sms_tool/payment_batch.py` | Pembayaran protokol batch, matriks kelayakan, Canary, retry, dan breakpoint atomik |
| `sms_tool/registration_progress.py` | Pelacakan dan persistensi progres tahap pendaftaran |
| `sms_tool/error_classification.py` | Klasifikasi jenis error dan normalisasi retry/laporan |

Penjelasan batas yang lebih rinci lihat [docs/architecture.md](docs/architecture.md), tanggung jawab direktori lihat [docs/directory-map.md](docs/directory-map.md).

## Konfigurasi Inti

### ReMail

```json
{
  "email_registration": {
    "remail": {
      "enabled": true,
      "base_url": "https://remail.aishop6.com",
      "api_key": "",
      "project_id": 2,
      "product_id": 5,
      "service_mode": "purchase",
      "supply": "private_first",
      "email_suffix": "outlook.com",
      "otp_poll_interval": 1,
      "batch_timeout": 200
    },
    "sentinel_max_concurrency": 2,
    "remail_otp_issued_after_grace_seconds": 90,
    "remail_otp_resend_after_seconds": 30
  }
}
```

### Proxy Pendaftaran dan Penerimaan

```json
{
  "mailbox_proxy": "http://127.0.0.1:7897",
  "proxy": {
    "registration": "http://user:pass-JP-session-5m@gateway:port",
    "default": "http://user:pass-JP-session-5m@gateway:port",
    "pool": ["http://user:pass-JP-session-5m@gateway:port"]
  }
}
```

Trafik pendaftaran melewati proxy dinamis JP (`proxy.registration` / `proxy.pool`), worker akan me-refresh Session dinamis sehingga IP exit tiap jalur paralel berbeda; penerimaan OTP email tetap melewati `mailbox_proxy` (default `http://127.0.0.1:7897`), tidak mewarisi proxy pendaftaran; trafik pembayaran melewati `paypal.stage_proxies` / `protocol_payments.proxy_pool` yang independen. Ketiganya tidak saling meng-overwrite, detail bisa dilihat dan diubah di konfigurasi proxy jaringan **Pengaturan → Jaringan & Pembayaran** di desktop.

### Pool Proxy Pembayaran Protokol

```json
{
  "protocol_payments": {
    "proxy_pool": [
      "http://user-region-JP-sid-session-t-5:pass@gateway-a:port",
      "http://user-region-JP-sid-session-t-10:pass@gateway-b:port"
    ]
  }
}
```

Pool pembayaran protokol dan pool proxy pendaftaran saling independen. Saat ekstraksi link akan menulis ulang `region-XX` atau negara dan Session dinamis di password sesuai wilayah pembayaran; hanya saat eksplisit mengirim `--proxy` atau proxy per segmen akan meng-override pool protokol.

### JIT AT dan Pembayaran Batch

```json
{
  "registration": {
    "at_stability_probe_count": 2,
    "at_stability_probe_delay_seconds": 10,
    "at_probe_timeout_seconds": 30,
    "stage_concurrency": { "network": 4, "at_probe": 4 }
  },
  "protocol_payments": {
    "batch": {
      "method_workers": { "momo": 2, "kakao": 2 },
      "pause_on_canary_failure": true,
      "canary_pause_seconds": 21600
    },
    "matrix": {
      "cells": [
        { "name": "vn_sticky", "payment_method": "momo", "registration_country": "VN", "checkout_country": "VN", "promotion_country": "VN", "provider_country": "VN", "approve_country": "VN", "redirect_country": "VN", "strategy": "custom_promo", "sample_size": 5 }
      ]
    }
  }
}
```

Akun pembayaran HTTP 401 di-recover dengan urutan: OAuth Refresh Token, Cookie yang ada `/api/auth/session`, OTP email browser terisolasi, Codex OAuth. Setiap kandidat AT hanya ditulis ke Session JSON dan SQLite jika terdeteksi HTTP 200 lagi. Konteks browser diisolasi per akun dan memvalidasi email login; `account_deactivated` diklasifikasikan sebagai kegagalan permanen, tidak akan login berulang.

### Impor SUB2API

```json
{
  "sub2api": {
    "auth_mode": "auto",
    "verify_after_import": true
  }
}
```

`auth_mode` bisa dipilih `auto`, `oauth`, `agent_identity`; Agent Identity hanya digunakan di batas impor SUB2API eksplisit. `verify_after_import` mengontrol apakah menjalankan validasi konektivitas setelah impor.

### Override Environment Variable Darurat

Saat OpenAI merotasi Stripe publishable key atau versi Sentinel SDK sehingga menyebabkan kegagalan ekstraksi link pembayaran atau OTP pendaftaran, bisa digunakan override sementara via environment variable tanpa mengubah kode:

- `PP_STRIPE_PUBLISHABLE_KEY`: override terpadu untuk Stripe publishable key fallback pembayaran protokol (digunakan bersama di dua tempat: `sms_tool/gen_pp_link.py` dan `services/protocol-payment/momo/ac_paylink_core.py`). Respons checkout biasanya sudah membawa key tersebut, nilai fallback hanya digunakan jika respons tidak ada; saat fallback akan dicetak log WARN.
- `OPENAI_SENTINEL_VERSION`: override versi Sentinel SDK (nilai default built-in di `sms_tool/sentinel_quickjs.py`). Jika download SDK mengembalikan 403/404 biasanya berarti versi saat ini sudah dirotasi dan tidak berlaku, cukup perbarui variabel ini atau `sentinel_version` di config.

Sebelum memulai bisa jalankan `python scripts/preflight_env.py` untuk memeriksa apakah Node.js, Playwright Chromium, dan paket Python utama sudah siap.

## Operasi Umum

### Pendaftaran Penerima Kode Jangka Pendek ReMail (hanya CLI)

```powershell
python chatgpt_phone_reg.py --remail-service-mode code --count 1 --workers 1 --registration-at-only --no-phone-reuse
```

### Pendaftaran Email Jangka Panjang ReMail dengan Verifikasi HP SMSBower

```powershell
python chatgpt_phone_reg.py --buy-remail-mailbox --remail-service-mode purchase --target-at200 40 --max-mailbox-purchases 80 --workers 10 --phone-reuse --phone-source smsbower
```

### Pendaftaran Protokol AT-Only Email Jangka Panjang ReMail

```powershell
python chatgpt_phone_reg.py --buy-remail-mailbox --remail-service-mode purchase --count 1 --workers 1 --registration-at-only --no-phone-reuse
```

Mode ini melewati Codex OAuth RT dan verifikasi HP, hanya dihitung sukses setelah Session tersimpan di disk dan deteksi AT mengembalikan HTTP 200.

### Pendaftaran Email CFWorker

```powershell
python chatgpt_phone_reg.py --buy-cfworker-mailbox --cfworker-domain example.com --count 1 --workers 1
```

### Pendaftaran dari File Email

```powershell
python chatgpt_phone_reg.py --chatai-mailbox-file hotmail.txt --count 4 --workers 4
```

### Menguji Exit Proxy Pembayaran

```powershell
python chatgpt_phone_reg.py --test-payment-proxies --checkout-proxy-country GB --approve-proxy-country JP --update-proxy-country BR
```

### Pembayaran Protokol Batch (bisa lanjut dari breakpoint)

```powershell
python chatgpt_phone_reg.py --extract-payment-link --payment-method momo --email-file runtime\eligible.txt --workers 2 --payment-batch-id momo_vn_20260731 --payment-canary 5 --payment-retries 1
```

### Ekstraksi Link Kartu Langsung Checkout

Ekstrak link Checkout PH/PHP jumlah nol:

```powershell
python chatgpt_phone_reg.py --extract-payment-link --payment-method direct_card --email user@example.com --proxy "http://proxy"
```

Menjalankan deteksi kapabilitas JIT AT, matriks ID, Checkout, dan Stripe init sebagai Canary akun tunggal GoPay; tidak akan membuat metode pembayaran atau mengirim Confirm/Approve:

```powershell
python chatgpt_phone_reg.py --extract-payment-link --payment-method gopay --email-file runtime\canary.txt --payment-probe-only --payment-canary 1 --payment-batch-id gopay_id_probe --workers 1
```

### Pendaftaran dan Otomatis Impor SUB2API

```powershell
python chatgpt_phone_reg.py --buy-remail-mailbox --count 1 --workers 1 --register-and-import --sub2api-auth-mode auto
```

### Melihat Parameter CLI

```powershell
python chatgpt_phone_reg.py --help
```

## Test, Build, dan Release

### Menjalankan Test

```powershell
python -m pytest -q
python -m compileall -q sms_tool
.\.dotnet\dotnet.exe test .\GPTRegisterTool.slnx -c Release
```

`global.json` mem-pin SDK repository, `Directory.Packages.props` mengelola versi NuGet secara terpusat, project xUnit standar berada di `tests/SmsWorkbench.Tests`. CI sekaligus menjalankan test Python, C#, dan release desktop yang standar.

### Compile Desktop

```powershell
powershell -ExecutionPolicy Bypass -File .\SmsWorkbench\build_dotnet.ps1
```

Direktori output standar:

```text
dist/net10/SmsWorkbench.exe
```

### Build Installer dan Paket Portabel

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_installer.ps1 -Version vYYYY.MM.DD
```

File release dikeluarkan ke `dist/release/`:

- Installer grafis Windows.
- Paket ZIP portabel.
- File checksum SHA-256.

Build dengan signature internal bisa menggunakan:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build_installer.ps1 -Version vYYYY.MM.DD -SelfSign
```

### Checklist Release

1. Pastikan `config.json`, kredensial email, password proxy, API Key, dan Token tidak masuk ke Git.
2. Jalankan test penuh, parsing konfigurasi contoh, pemeriksaan compile Python, dan `git diff --check`.
3. Perbarui `dist/net10` menggunakan satu-satunya script compile yang didukung.
4. Build installer, paket portabel, dan file checksum, lalu periksa ulang SHA-256 di checklist verifikasi.
5. Pastikan commit yang akan di-release sudah di-push, dan `git status --short` kosong; data lokal yang diabaikan seperti `runtime/`, `sessions/` tidak masuk ke commit release.
6. Buat tag versi pada commit tersebut dan upload aset Release yang dihasilkan dari build yang sama.
7. Judul dan isi GitHub Release seragam menggunakan bahasa Indonesia; perintah, nama file, dan kode error dipertahankan dalam format asli.

Release saat ini menggunakan `vYYYY.MM.DD`; revisi dokumen atau build di hari yang sama menggunakan tag patch seperti `vYYYY.MM.DD.1`. Installer, ZIP portabel, dan file SHA-256 harus berasal dari build `scripts/build_installer.ps1` yang sama, dan validasi digest sebelum upload. Aset release tetap: `GPT-Register-Tool-Setup-<version>.exe`, `GPT-Register-Tool-win-x64-<version>.zip`, dan `GPT-Register-Tool-<version>.sha256.txt`.

## Data dan Keamanan

- `config.json`, `sessions/`, `runtime/`, pool email, dan file token secara default diabaikan oleh Git.
- Konfigurasi contoh tidak mengandung API Key, kredensial email, atau password proxy yang sebenarnya.
- ReMail API Key dan Service Token akan di-mask di exception dan log.
- Link pembayaran, BA Token, AT/RT akun, dan kredensial email semuanya termasuk data sensitif, tidak boleh dibagikan secara publik.
- Ketersediaan dan biaya layanan pihak ketiga (email, pembayaran, proxy, penerima kode) ditentukan oleh penyedia layanan masing-masing.

## Indeks Dokumen

- [Penjelasan Arsitektur](docs/architecture.md)
- [Tanggung Jawab Direktori](docs/directory-map.md)
- [Penjelasan Link PayPal 0 Yuan](docs/paypal-zero-due-link.md)
- [Catatan Release Terbaru](docs/release-v2026.08.09.md)
- [Panduan Proxy](PROXY_GUIDE.md)

## Lisensi dan Tanggung Jawab Penggunaan

Harap gunakan proyek ini hanya dalam skenario yang sudah mendapat otorisasi dan sesuai dengan syarat layanan terkait, regulasi wilayah, serta kebijakan organisasi. Pengguna bertanggung jawab sendiri atas biaya layanan pihak ketiga, keamanan akun, dan kepatuhan data.
