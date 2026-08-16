namespace SmsWorkbench
{
    public sealed record ProtocolPaymentExecutionRequest(
        string PaymentMethod,
        string TargetCountry,
        string Proxy,
        bool JitRefresh,
        bool ProbeOnly,
        bool RequireZero,
        bool RequireBaToken,
        string BlikCode,
        string CheckoutCountry,
        string ApproveCountry,
        string UpdateCountry,
        string AccountEmail,
        string SessionFile);

    public sealed record ProtocolPaymentExecutionPlan(
        string TaskName,
        string StatusText,
        IReadOnlyList<string> Arguments,
        string Operation,
        bool MayHaveSideEffects);

    public static class ProtocolPaymentExecutionPlanner
    {
        public static ProtocolPaymentExecutionPlan Create(ProtocolPaymentExecutionRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            string method = PaymentMethods.Normalize(request.PaymentMethod);
            string accountEmail = (request.AccountEmail ?? "").Trim();
            string sessionFile = (request.SessionFile ?? "").Trim();
            if (accountEmail.Length == 0 && sessionFile.Length == 0)
                throw new InvalidOperationException("Pembayaran perjanjian memerlukan file akun atau Sesi");

            var arguments = new List<string>
            {
                "--extract-payment-link",
                "--payment-method", method,
                "--target-country", Country(request.TargetCountry, "US"),
            };

            if (accountEmail.Length > 0)
            {
                arguments.AddRange(new[] { "--email", accountEmail });
                if (sessionFile.Length > 0)
                    arguments.AddRange(new[] { "--session-file", sessionFile });
            }
            else
            {
                arguments.AddRange(new[] { "--session-file", sessionFile });
            }

            string proxy = (request.Proxy ?? "").Trim();
            if (proxy.Length > 0)
                arguments.AddRange(new[] { "--proxy", proxy });

            if (accountEmail.Length > 0 && !request.JitRefresh)
                arguments.Add("--no-jit-at-refresh");
            if (request.ProbeOnly)
                arguments.Add("--payment-probe-only");

            AddCountryArgument(arguments, "--checkout-proxy-country", request.CheckoutCountry);
            AddCountryArgument(arguments, "--approve-proxy-country", request.ApproveCountry);
            AddCountryArgument(arguments, "--update-proxy-country", request.UpdateCountry);

            if (!request.RequireZero)
                arguments.Add("--no-require-zero");
            if (method == "paypal" && request.RequireBaToken)
                arguments.Add("--require-ba-token");
            string blikCode = (request.BlikCode ?? "").Trim();
            if (!request.ProbeOnly && method == "blik" && blikCode.Length > 0)
                arguments.AddRange(new[] { "--blik-code", blikCode });

            string methodLabel = PaymentMethods.DisplayName(method);
            bool mayHaveSideEffects = !request.ProbeOnly && method != "direct_card";
            if (request.ProbeOnly)
            {
                return new ProtocolPaymentExecutionPlan(
                    methodLabel + " deteksi kemampuan pembayaran",
                    "Menjalankan " + methodLabel + " Checkout / Stripe init kemampuan deteksi...",
                    arguments,
                    "payment_method_capability_probe",
                    false);
            }
            if (method == "blik")
            {
                return new ProtocolPaymentExecutionPlan(
                    methodLabel + " pembayaran protokol",
                    "Menjalankan " + methodLabel + " pembayaran protokol...",
                    arguments,
                    "execute_payment",
                    true);
            }
            return new ProtocolPaymentExecutionPlan(
                methodLabel + " ekstraksi chain protokol",
                "Menjalankan " + methodLabel + " ekstraksi chain protokol...",
                arguments,
                "extract_link",
                mayHaveSideEffects);
        }

        public static IReadOnlyList<string> CreateProxyTestArguments(
            string paymentMethod,
            string proxy,
            string checkoutCountry,
            string approveCountry,
            string updateCountry)
        {
            string method = PaymentMethods.Normalize(paymentMethod);
            var arguments = new List<string>
            {
                "--test-payment-proxies",
                "--payment-method",
                method,
            };
            string proxyValue = (proxy ?? "").Trim();
            if (proxyValue.Length > 0)
                arguments.AddRange(new[] { "--proxy", proxyValue });
            AddCountryArgument(arguments, "--checkout-proxy-country", checkoutCountry);
            AddCountryArgument(arguments, "--approve-proxy-country", approveCountry);
            AddCountryArgument(arguments, "--update-proxy-country", updateCountry);
            return arguments;
        }

        private static void AddCountryArgument(List<string> arguments, string option, string country)
        {
            string normalized = Country(country, "");
            if (normalized.Length > 0)
                arguments.AddRange(new[] { option, normalized });
        }

        private static string Country(string value, string fallback)
        {
            string normalized = (value ?? "").Trim().ToUpperInvariant();
            return normalized.Length > 0 ? normalized : fallback;
        }

    }

    public sealed record ProtocolPaymentResultPresentation(
        string Text,
        string Url,
        string QrPath,
        string TerminalState = "",
        bool Retryable = false,
        bool RequiresReconciliation = false,
        string Operation = "");

    public static class ProtocolPaymentResultPresenter
    {
        public static ProtocolPaymentResultPresentation Parse(string result)
        {
            string rawResult = result ?? "";
            try
            {
                using JsonDocument json = JsonDocument.Parse(rawResult);
                JsonElement root = json.RootElement;
                bool ok = root.TryGetProperty("ok", out JsonElement okElement)
                    && okElement.ValueKind == JsonValueKind.True;
                string operation = StringValue(root, "operation");
                if (!ok)
                    return Failed(root, operation);

                string terminalState = TerminalState(root);
                if (terminalState is "cancelled" or "unknown" or "timed_out")
                    return Failed(root, operation);

                var text = new StringBuilder();
                bool paymentCompleted = operation == "execute_payment"
                    && string.Equals(StringValue(root, "status"), "completed", StringComparison.OrdinalIgnoreCase);
                bool capabilityCompleted = operation == "payment_method_capability_probe";
                text.AppendLine(paymentCompleted
                    ? "[Sukses] Pembayaran selesai"
                    : capabilityCompleted ? "[Sukses] Deteksi kemampuan selesai" : "[Sukses] Ekstraksi berhasil!");
                text.AppendLine();

                AppendNonEmptyString(text, root, "message", "", rejectWhitespace: true);
                if (root.TryGetProperty("probe", out JsonElement probe) && probe.ValueKind == JsonValueKind.Object)
                {
                    string probeStatus = probe.TryGetProperty("status_code", out JsonElement statusCode)
                        ? statusCode.ToString()
                        : "";
                    if (probeStatus.Length > 0)
                        text.AppendLine(CultureInfo.InvariantCulture, $"AT Probe: HTTP {probeStatus}");
                }
                if (root.TryGetProperty("refreshed", out JsonElement refreshed)
                    && refreshed.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    text.AppendLine(CultureInfo.InvariantCulture, $"Refresh JIT: {(refreshed.GetBoolean() ? "AT baru berhasil didapat" : "Belum dimuat ulang")}");
                if (root.TryGetProperty("token_telemetry", out JsonElement telemetry)
                    && telemetry.ValueKind == JsonValueKind.Object)
                {
                    if (telemetry.TryGetProperty("age_seconds", out JsonElement age))
                        text.AppendLine(CultureInfo.InvariantCulture, $"AT Usia: {age} detik");
                    if (telemetry.TryGetProperty("expires_in_seconds", out JsonElement expiresIn))
                        text.AppendLine(CultureInfo.InvariantCulture, $"AT Tersisa: {expiresIn} detik");
                }

                string url = "";
                if (root.TryGetProperty("upi_uri", out JsonElement upiUri)
                    && !string.IsNullOrEmpty(upiUri.GetString()))
                {
                    url = upiUri.GetString() ?? "";
                    text.AppendLine(CultureInfo.InvariantCulture, $"UPI URI: {SensitiveDataSanitizer.Redact(url)}");
                }
                else if (root.TryGetProperty("url", out JsonElement urlElement)
                    && !string.IsNullOrEmpty(urlElement.GetString()))
                {
                    url = urlElement.GetString() ?? "";
                    text.AppendLine(CultureInfo.InvariantCulture, $"Tautan: {SensitiveDataSanitizer.Redact(url)}");
                }

                AppendString(text, root, "hosted_url", "URL host: ");
                AppendString(text, root, "link_type", "Tipe Tautan: ");
                AppendString(text, root, "run_id", "ID Tugas: ");
                AppendString(text, root, "manager_state", "Mesin status: ");
                AppendString(text, root, "state", "Status eksekusi: ");
                AppendString(text, root, "operation", "Aksi eksekusi: ");
                AppendString(text, root, "subscription_plan", "Status langganan: ");
                AppendString(text, root, "payment_method", "Metode pembayaran: ");

                if (root.TryGetProperty("card_last4", out JsonElement last4)
                    && !string.IsNullOrWhiteSpace(last4.GetString()))
                    text.AppendLine("Kartu: [REDACTED]");

                string qrPath = root.TryGetProperty("qr_path", out JsonElement qrPathElement)
                    ? qrPathElement.GetString() ?? ""
                    : "";
                if (qrPath.Length > 0)
                    text.AppendLine(CultureInfo.InvariantCulture, $"Gambar QR: {qrPath}");

                AppendString(text, root, "cs_id", "CS ID: ");
                if (root.TryGetProperty("amount", out JsonElement amount))
                    text.AppendLine(CultureInfo.InvariantCulture, $"Jumlah: {amount}");
                AppendString(text, root, "currency", "Mata Uang: ");
                AppendNonEmptyString(text, root, "coupon_name", "Kupon: ", rejectWhitespace: false);
                if (root.TryGetProperty("approval_ok", out JsonElement approval))
                    text.AppendLine(CultureInfo.InvariantCulture, $"Status Persetujuan: {(approval.GetBoolean() ? "Disetujui" : "Belum Diproses/Gagal")}");
                if (root.TryGetProperty("expires_at", out JsonElement expiresAt))
                {
                    try
                    {
                        long expires = expiresAt.GetInt64();
                        if (expires > 0)
                        {
                            DateTime local = DateTimeOffset.FromUnixTimeSeconds(expires).LocalDateTime;
                            text.AppendLine(CultureInfo.InvariantCulture, $"Waktu kedaluwarsa: {local:yyyy-MM-dd HH:mm:ss}");
                        }
                    }
                    catch
                    {
                    }
                }
                AppendString(text, root, "target_country", "Negara: ");
                AppendString(text, root, "warning", "Peringatan: ");

                return new ProtocolPaymentResultPresentation(
                    text.ToString().TrimEnd(),
                    url,
                    qrPath,
                    "completed",
                    false,
                    false,
                    operation);
            }
            catch
            {
                return new ProtocolPaymentResultPresentation(SensitiveDataSanitizer.Redact(rawResult), "", "");
            }
        }

        public static ProtocolPaymentResultPresentation Aborted(
            ProtocolPaymentExecutionPlan plan,
            string requestedState)
        {
            string requested = CanonicalState(requestedState);
            if (requested.Length == 0)
                requested = "failed";
            bool requiresReconciliation = plan?.MayHaveSideEffects == true;
            string terminalState = requiresReconciliation ? "unknown" : requested;
            bool retryable = terminalState == "timed_out";
            string operation = plan?.Operation ?? "";
            string text = terminalState switch
            {
                "unknown" => "[Hasil tidak diketahui, silakan cek status akun dulu, jangan coba ulang]",
                "cancelled" => "[Dibatalkan] Tugas pembayaran protokol telah dihentikan",
                "timed_out" => "[Waktu habis] Tugas pembayaran protokol waktu habis, dapat dicoba ulang sesuai strategi",
                _ => "[Gagal] Tugas pembayaran protokol belum selesai"
            };
            if (operation.Length > 0)
                text += $"\nAksi yang dijalankan: {operation}";
            if (requiresReconciliation)
                text += "\nPerlu rekonsiliasi: permintaan mungkin telah mencapai layanan pembayaran.";
            return new ProtocolPaymentResultPresentation(
                text,
                "",
                "",
                terminalState,
                retryable,
                requiresReconciliation,
                operation);
        }

        private static ProtocolPaymentResultPresentation Failed(JsonElement root, string operation)
        {
            string error = SensitiveDataSanitizer.Redact(StringValue(root, "error"));
            string errorCode = StringValue(root, "error_code");
            string state = TerminalState(root);
            if (state.Length == 0)
                state = "failed";
            bool requiresReconciliation = state == "unknown"
                || BoolValue(root, "requires_reconciliation")
                || BoolValue(root, "outcome_unknown");
            bool retryable = BoolValue(root, "retryable")
                && !requiresReconciliation
                && state != "cancelled";
            string prefix = state switch
            {
                "unknown" => "[Hasil tidak diketahui, silakan cek status akun dulu, jangan coba ulang]",
                "cancelled" => "[Dibatalkan]",
                "timed_out" => "[Timeout]",
                _ => "[Gagal]"
            };
            string text = $"{prefix} {error}".TrimEnd()
                + (errorCode.Length == 0 ? "" : $"\nKode galat: {errorCode}");
            string errorStage = StringValue(root, "error_stage");
            if (errorStage.Length > 0)
                text += $"\nTahap galat: {errorStage}";
            if (requiresReconciliation)
                text += "\nPerlu rekonsiliasi: permintaan mungkin telah mencapai layanan pembayaran.";
            else if (retryable)
                text += "\nDapat dicoba ulang: Ya";
            return new ProtocolPaymentResultPresentation(
                text,
                "",
                "",
                state,
                retryable,
                requiresReconciliation,
                operation);
        }

        private static string TerminalState(JsonElement root)
        {
            if (BoolValue(root, "requires_reconciliation") || BoolValue(root, "outcome_unknown"))
                return "unknown";
            foreach (string property in new[] { "terminal_state", "status", "state", "outcome", "manager_state" })
            {
                string state = CanonicalState(StringValue(root, property));
                if (state.Length > 0)
                    return state;
            }
            return "";
        }

        private static string CanonicalState(string value)
        {
            string normalized = (value ?? "").Trim().ToLowerInvariant()
                .Replace('-', '_')
                .Replace(' ', '_');
            if (normalized is "cancelled" or "canceled" or "cancelled_by_user" or "canceled_by_user"
                or "interrupted" or "keyboard_interrupt")
                return "cancelled";
            if (normalized is "timed_out" or "timeout" or "timeout_expired" or "extractor_timeout")
                return "timed_out";
            if (normalized is "unknown" or "outcome_unknown" or "payment_outcome_unknown"
                or "indeterminate" or "inconclusive")
                return "unknown";
            return "";
        }

        private static string StringValue(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value))
                return "";
            return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
        }

        private static bool BoolValue(JsonElement root, string propertyName)
        {
            return root.TryGetProperty(propertyName, out JsonElement value)
                && value.ValueKind == JsonValueKind.True;
        }

        private static void AppendString(StringBuilder text, JsonElement root, string propertyName, string prefix)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value))
                return;
            text.AppendLine(prefix + SensitiveDataSanitizer.Redact(value.GetString()));
        }

        private static void AppendNonEmptyString(
            StringBuilder text,
            JsonElement root,
            string propertyName,
            string prefix,
            bool rejectWhitespace)
        {
            if (!root.TryGetProperty(propertyName, out JsonElement value))
                return;
            string content = value.GetString() ?? "";
            bool empty = rejectWhitespace ? string.IsNullOrWhiteSpace(content) : content.Length == 0;
            if (!empty)
                text.AppendLine(prefix + SensitiveDataSanitizer.Redact(content));
        }
    }
}
