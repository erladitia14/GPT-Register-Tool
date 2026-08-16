using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace SmsWorkbench
{
    public sealed partial class PaymentBatchViewModel : ObservableObject
    {
        private readonly IPaymentBatchService _paymentBatchService;
        private readonly IFileLauncher _fileLauncher;
        private readonly PaymentBatchAccount[] _accounts;
        private string _automaticBatchId;

        [ObservableProperty] private PaymentMethodOption selectedMethod;
        [ObservableProperty] private int workers = 2;
        [ObservableProperty] private int retries = 1;
        [ObservableProperty] private string canaryText = "0";
        [ObservableProperty] private string batchId = "";
        [ObservableProperty] private string proxy = "";
        [ObservableProperty] private bool jitRefresh = true;
        [ObservableProperty] private bool probeOnly;
        [ObservableProperty] private bool requireZero = true;
        [ObservableProperty] private PaymentMatrixRow selectedMatrixRow;
        [ObservableProperty] private string status = "Siap";
        [ObservableProperty] private string reportPath = "";
        [ObservableProperty] private bool isRunning;
        [ObservableProperty] private bool hasRun;

        public PaymentBatchViewModel(
            IPaymentBatchService paymentBatchService,
            IFileLauncher fileLauncher,
            IEnumerable<PaymentBatchAccount> accounts)
        {
            _paymentBatchService = paymentBatchService;
            _fileLauncher = fileLauncher;
            _accounts = (accounts ?? Array.Empty<PaymentBatchAccount>())
                .Where(account => !string.IsNullOrWhiteSpace(account.Email))
                .GroupBy(account => account.Email.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First() with { Email = group.Key })
                .ToArray();
            PaymentMethodOptions = PaymentMethods.BatchOptions;
            WorkerOptions = Enumerable.Range(1, 10).ToArray();
            RetryOptions = new[] { 0, 1, 2 };
            selectedMethod = PaymentMethodOptions.First(option => option.Id == "momo");
            _automaticBatchId = CreateBatchId(selectedMethod.Id);
            batchId = _automaticBatchId;
            ReloadMatrix();
        }

        public IReadOnlyList<PaymentMethodOption> PaymentMethodOptions { get; }

        public IReadOnlyList<int> WorkerOptions { get; }

        public IReadOnlyList<int> RetryOptions { get; }

        public ObservableCollection<PaymentMatrixRow> MatrixRows { get; } = new();

        public ObservableCollection<PaymentBatchResultRow> Results { get; } = new();

        public string AccountSummary => $"Akun {_accounts.Length} · AT diperoleh {_accounts.Count(account => account.HasAccessToken)}";

        public bool RequireZeroEnabled => !ProbeOnly;

        private bool CanRun() => !IsRunning && _accounts.Length > 0;

        private bool CanDeleteMatrixRow() => !IsRunning && SelectedMatrixRow != null && MatrixRows.Count > 1;

        private bool CanOpenReport() => !IsRunning && _fileLauncher.Exists(ReportPath);

        partial void OnSelectedMethodChanged(PaymentMethodOption value)
        {
            if (value == null) return;
            if (string.IsNullOrWhiteSpace(BatchId) || string.Equals(BatchId, _automaticBatchId, StringComparison.Ordinal))
            {
                _automaticBatchId = CreateBatchId(value.Id);
                BatchId = _automaticBatchId;
            }
            OnPropertyChanged(nameof(RequireZeroEnabled));
            ReloadMatrix();
        }

        partial void OnProbeOnlyChanged(bool value)
        {
            OnPropertyChanged(nameof(RequireZeroEnabled));
        }

        partial void OnSelectedMatrixRowChanged(PaymentMatrixRow value) => DeleteMatrixRowCommand.NotifyCanExecuteChanged();

        partial void OnReportPathChanged(string value) => OpenReportCommand.NotifyCanExecuteChanged();

        partial void OnIsRunningChanged(bool value)
        {
            RunCommand.NotifyCanExecuteChanged();
            DeleteMatrixRowCommand.NotifyCanExecuteChanged();
            OpenReportCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand]
        private void AddMatrixRow()
        {
            MatrixRows.Add(_paymentBatchService.CreateDefaultMatrixRow(SelectedMethod?.Id ?? "paypal"));
            DeleteMatrixRowCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanDeleteMatrixRow))]
        private void DeleteMatrixRow()
        {
            if (SelectedMatrixRow == null || MatrixRows.Count <= 1) return;
            MatrixRows.Remove(SelectedMatrixRow);
            SelectedMatrixRow = null;
            DeleteMatrixRowCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanOpenReport))]
        private void OpenReport() => _fileLauncher.Open(ReportPath);

        [RelayCommand]
        private void CopyResult(PaymentBatchResultRow row)
        {
            if (row == null || !row.HasCopyableResult) return;
            try
            {
                Clipboard.SetText(row.ResultValue);
                Status = $"Telah disalin{row.ResultKind}：{row.AccountRef}";
            }
            catch (Exception exception)
            {
                Status = "Gagal menyalin:" + exception.Message;
            }
        }

        [RelayCommand(IncludeCancelCommand = true, CanExecute = nameof(CanRun))]
        private async Task RunAsync(CancellationToken cancellationToken)
        {
            if (!TryCreateRequest(out PaymentBatchRequest request)) return;
            Results.Clear();
            ReportPath = "";
            Status = ProbeOnly
                ? "Menjalankan deteksi kemampuan pembayaran Checkout dan Stripe init..."
                : "Menjalankan deteksi JIT dan batch pembayaran protokol...";
            IsRunning = true;
            try
            {
                JsonElement report = await _paymentBatchService.RunAsync(request, cancellationToken);
                HasRun = true;
                PopulateResults(report);
                ReportPath = JsonString(report, "report_path");
                string error = JsonString(report, "error");
                Status = error.Length > 0 && !report.TryGetProperty("counts", out _)
                    ? "Eksekusi gagal:" + error
                    : FormatSummary(report);
            }
            catch (OperationCanceledException)
            {
                Status = request.ProbeOnly
                    ? "Dibatalkan."
                    : "Hasil tidak diketahui, periksa dulu breakpoint batch dan status layanan pembayaran, jangan coba lagi.";
            }
            catch (TimeoutException)
            {
                Status = request.ProbeOnly
                    ? "Deteksi kemampuan timeout, dapat dicoba ulang sesuai strategi."
                    : "Hasil tidak diketahui, periksa dulu breakpoint batch dan status layanan pembayaran, jangan coba lagi.";
            }
            catch (Exception exception)
            {
                Status = "Eksekusi gagal:" + exception.Message;
            }
            finally
            {
                IsRunning = false;
            }
        }

        private bool TryCreateRequest(out PaymentBatchRequest request)
        {
            request = null;
            if (!int.TryParse(CanaryText.Trim(), out int canary) || canary < 0)
            {
                Status = "Jumlah Canary harus bilangan bulat non-negatif.";
                return false;
            }
            string normalizedBatchId = Regex.Replace((BatchId ?? "").Trim(), @"[^A-Za-z0-9_.-]+", "_");
            if (normalizedBatchId.Length == 0)
            {
                Status = "Masukkan ID batch.";
                return false;
            }
            if (MatrixRows.Any(cell => !cell.IsValid()))
            {
                Status = "Kode negara matriks harus kosong atau Dua huruf, jumlah sampel harus lebih besar dari 0.";
                return false;
            }
            BatchId = normalizedBatchId;
            request = new PaymentBatchRequest(
                _accounts,
                SelectedMethod?.Id ?? "paypal",
                Workers,
                Retries,
                canary,
                normalizedBatchId,
                Proxy ?? "",
                JitRefresh,
                ProbeOnly,
                RequireZero,
                MatrixRows.ToArray());
            return true;
        }

        private void ReloadMatrix()
        {
            if (_paymentBatchService == null || SelectedMethod == null) return;
            MatrixRows.Clear();
            IReadOnlyList<PaymentMatrixRow> configured = _paymentBatchService.LoadMatrix(SelectedMethod.Id);
            foreach (PaymentMatrixRow row in configured.Count > 0
                ? configured
                : new[] { _paymentBatchService.CreateDefaultMatrixRow(SelectedMethod.Id) })
            {
                MatrixRows.Add(row);
            }
            DeleteMatrixRowCommand.NotifyCanExecuteChanged();
        }

        private void PopulateResults(JsonElement report)
        {
            if (!report.TryGetProperty("results", out JsonElement values) || values.ValueKind != JsonValueKind.Array) return;
            foreach (JsonElement row in values.EnumerateArray())
            {
                string eligibility = "Tidak Dikenal";
                if (row.TryGetProperty("eligible", out JsonElement eligible)
                    && eligible.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    eligibility = eligible.GetBoolean() ? "Sesuai" : "Tidak memenuhi syarat";
                string decision = JsonString(row, "decision");
                string paymentUrl = FirstNonEmpty(JsonString(row, "url"), JsonString(row, "long_url"));
                string qrData = JsonString(row, "qr_data");
                string qrPath = JsonString(row, "qr_path");
                string terminalState = FirstNonEmpty(
                    JsonString(row, "terminal_state"),
                    JsonString(row, "status"),
                    JsonString(row, "state"));
                if (terminalState.Equals("canceled", StringComparison.OrdinalIgnoreCase))
                    terminalState = "cancelled";
                string resultKind = paymentUrl.Length > 0
                    ? "Tautan pembayaran"
                    : qrData.Length > 0
                        ? "Konten QR"
                        : qrPath.Length > 0 ? "File QR" : "";
                string resultValue = FirstNonEmpty(paymentUrl, qrData, qrPath);
                Results.Add(new PaymentBatchResultRow
                {
                    AccountRef = JsonString(row, "account_ref"),
                    MatrixCell = JsonString(row, "matrix_cell"),
                    AuthStatus = JsonBool(row, "authenticated") ? "200" : "Gagal",
                    RefreshStatus = JsonBool(row, "refreshed") ? "Telah Disegarkan" : "Belum dimuat ulang",
                    Eligibility = eligibility,
                    Decision = decision.Length > 0 ? decision : JsonString(row, "error"),
                    TerminalState = terminalState,
                    ErrorStage = JsonString(row, "error_stage"),
                    Retryable = JsonBool(row, "retryable"),
                    ResultKind = resultKind,
                    ResultValue = resultValue,
                    Attempts = JsonInt(row, "attempts")
                });
            }
        }

        private static string FormatSummary(JsonElement report)
        {
            if (!report.TryGetProperty("counts", out JsonElement counts) || counts.ValueKind != JsonValueKind.Object)
                return "Batch telah berakhir, tetapi jumlah tidak dikembalikan.";
            return $"Permintaan {JsonInt(counts, "requested")}  ·  AT 200 {JsonInt(counts, "authenticated")}"
                + $"  ·  JIT {JsonInt(counts, "refreshed")}  ·  Kualifikasi {JsonInt(counts, "eligible")}"
                + $"  ·  Selesai {JsonInt(counts, "completed")}  ·  Tautan {JsonInt(counts, "link_ready")}"
                + $"  ·  QR kode {JsonInt(counts, "qr_ready")}  ·  Batalkan {JsonInt(counts, "cancelled")}"
                + $"  ·  Tidak diketahui {JsonInt(counts, "unknown")}  ·  Waktu habis {JsonInt(counts, "timed_out")}"
                + $"  ·  Gagal {JsonInt(counts, "failed")}  ·  Dapat dicoba ulang {JsonInt(counts, "retryable")}"
                + $"  ·  Resume checkpoint {JsonInt(report, "resumed")}";
        }

        private static string JsonString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement value)) return "";
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        }

        private static int JsonInt(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement value)) return 0;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number)) return number;
            return int.TryParse(value.ToString(), out number) ? number : 0;
        }

        private static bool JsonBool(JsonElement element, string name)
            => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.True;

        private static string FirstNonEmpty(params string[] values)
            => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";

        private static string CreateBatchId(string paymentMethod)
            => PaymentMethods.Normalize(paymentMethod) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
    }
}
