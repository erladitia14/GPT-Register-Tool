namespace SmsWorkbench
{
    public partial class MainWindow
    {
        // ── Search clear button ──

        private void SearchClear_Click(object sender, RoutedEventArgs e)
        {
            SearchText = "";
            UpdateSearchClearVisibility();
        }

        /// <summary>
        /// Toggle the visibility of the search clear (×) button based on
        /// whether the search text is non-empty. Called from the SearchText
        /// setter and from the clear button click handler.
        /// </summary>
        private void UpdateSearchClearVisibility()
        {
            if (SearchClearButton != null)
            {
                SearchClearButton.Visibility = string.IsNullOrEmpty(SearchText)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        // ── DataGrid context menu handlers ──

        private void CtxViewDetail_Click(object sender, RoutedEventArgs e)
        {
            if (AccountGrid?.SelectedItem is PoolRow row)
                ShowAccountDetail(row);
        }

        private void CtxViewInbox_Click(object sender, RoutedEventArgs e)
        {
            if (AccountGrid?.SelectedItem is PoolRow row)
                ShowInboxDialog(row);
        }

        private void CtxCopyEmail_Click(object sender, RoutedEventArgs e)
        {
            if (AccountGrid?.SelectedItem is PoolRow row && !string.IsNullOrWhiteSpace(row.Identifier))
            {
                try
                {
                    Clipboard.SetText(row.Identifier);
                    NotifyInfo("Email disalin: " + row.Identifier);
                }
                catch (Exception ex)
                {
                    Log("Gagal menyalin email:" + ex.Message);
                }
            }
        }

        private void CtxCopyPayPal_Click(object sender, RoutedEventArgs e)
        {
            if (AccountGrid?.SelectedItem is PoolRow row && !string.IsNullOrWhiteSpace(row.PayPalUrl))
            {
                CopyPayPalUrl(row.PayPalUrl, row.Identifier);
            }
            else
            {
                NotifyWarning("Baris terpilih tidak memiliki tautan pembayaran.");
            }
        }

        private void CtxOpenPayPal_Click(object sender, RoutedEventArgs e)
        {
            if (AccountGrid?.SelectedItem is PoolRow row && !string.IsNullOrWhiteSpace(row.PayPalUrl))
            {
                OpenPayPalUrl(row.PayPalUrl, row.Identifier);
            }
            else
            {
                NotifyWarning("Baris terpilih tidak memiliki tautan pembayaran.");
            }
        }

        private void CtxOpenSource_Click(object sender, RoutedEventArgs e)
        {
            if (AccountGrid?.SelectedItem is PoolRow row)
                OpenAccountJson(row);
        }

        private void CtxMarkPayPal_Click(object sender, RoutedEventArgs e)
        {
            if (AccountGrid?.SelectedItem is PoolRow row)
                MarkPayPalComplete(row);
        }

        private async void CtxCheckAccountAlive_Click(object sender, RoutedEventArgs e)
        {
            if (AccountGrid?.SelectedItem is not PoolRow row || string.IsNullOrWhiteSpace(row.Identifier))
            {
                NotifyWarning("Pilih akun terlebih dahulu.");
                return;
            }
            await CheckAccountAliveAsync(row);
        }

        private void CtxBatchProtocolPayment_Click(object sender, RoutedEventArgs e)
        {
            BatchProtocolPayment_Click(sender, e);
        }

        private async Task CheckAccountAliveAsync(PoolRow row)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.Identifier))
            {
                NotifyWarning("Pilih akun terlebih dahulu.");
                return;
            }

            if (!row.HasAccessToken)
            {
                await DialogFactory.ShowInfoAsync(this, "Tes Akun Aktif", "Akun ini belum mendapatkan Access Token, tidak bisa diuji kelayakan. Silakan login terlebih dahulu untuk mendapatkan AT.");
                return;
            }

            try
            {
                Log($"Melakukan pengecekan aktivitas akun: {row.Identifier}");
                var args = new List<string> { "--quota-usage", "--email", row.Identifier, "--refresh-timeout", "45" };
                AddRegistrationProxy(args);
                string json = await Task.Run(() => RunBackendWithResult("Tes Akun Aktif", args));

                if (string.IsNullOrWhiteSpace(json))
                {
                    await DialogFactory.ShowInfoAsync(this, "Tes Akun Aktif", "Tes Aktivasi Akun gagal: tidak menerima respons yang valid.");
                    return;
                }

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("ok", out var okEl) && okEl.GetBoolean())
                {
                    string detail = FormatAccountLivenessDetail(root);
                    await DialogFactory.ShowInfoAsync(this, $"Uji aktivasi akun: {row.Identifier}", detail);
                    Log($"Tes Aktivasi Akun berhasil: {row.Identifier} → AT valid");
                    RefreshPools();
                }
                else
                {
                    string error = root.TryGetProperty("error", out var errEl) ? errEl.GetString() ?? "Kesalahan tidak dikenal" : "Kesalahan tidak dikenal";
                    string status = root.TryGetProperty("status", out var stEl) ? stEl.GetString() ?? "" : "";
                    string msg = $"Pengecekan aktivitas gagal: {error}";
                    if (status == "token_invalid")
                        msg += "\n\nAntarmuka mengembalikan HTTP 401, Access Token saat ini telah kedaluwarsa.";
                    await DialogFactory.ShowInfoAsync(this, $"Uji aktivasi akun: {row.Identifier}", msg);
                    Log($"Tes Aktivasi Akun gagal: {row.Identifier} → {error}");
                }
            }
            catch (Exception ex)
            {
                Log($"Pengecualian Tes Aktivasi Akun: {ex.Message}");
                await DialogFactory.ShowInfoAsync(this, "Tes Akun Aktif", $"Eksepsi pengecekan aktivitas: {ex.Message}");
            }
        }

        private static string FormatAccountLivenessDetail(JsonElement root)
        {
            var sb = new StringBuilder();
            string statusCode = root.TryGetProperty("status_code", out var codeEl) ? codeEl.ToString() : "";
            sb.AppendLine("Status: AT valid");
            sb.AppendLine("Antarmuka: HTTP " + (string.IsNullOrWhiteSpace(statusCode) ? "200" : statusCode));
            return sb.ToString().TrimEnd();
        }
    }
}
