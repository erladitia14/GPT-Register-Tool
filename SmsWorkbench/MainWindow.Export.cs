namespace SmsWorkbench
{
    public partial class MainWindow
    {
        // Account import/export, scan result and export JSON helpers
        private void ImportPaidCpa_Click(object sender, RoutedEventArgs e)
        {
            string target = ShowImportTargetDialog("Impor Sekali Klik");
            if (target.Length == 0) return;

            var selected = SelectedRowsOrCurrent()
                .Where(IsImportableAccountRow)
                .Where(r => !string.IsNullOrWhiteSpace(r.Identifier))
                .GroupBy(r => r.Identifier.Trim().ToLowerInvariant())
                .Select(g => g.First())
                .ToList();
            var rows = selected.Count > 0
                ? selected
                : allRows.Where(IsImportableAccountRow)
                    .Where(r => !string.IsNullOrWhiteSpace(r.Identifier))
                    .GroupBy(r => r.Identifier.Trim().ToLowerInvariant())
                    .Select(g => g.First())
                    .ToList();

            if (rows.Count == 0)
            {
                MessageBox.Show("Tidak menemukan akun yang dapat diimpor. Harap registrasi akun terlebih dahulu dan dapatkan access_token/session.", "Impor Sekali Klik", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string emailFile = Path.Combine(Path.GetTempPath(), "oneclick_import_emails_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllLines(emailFile, rows.Select(r => r.Identifier.Trim()), new UTF8Encoding(false));
            var args = new List<string> { "--import-cpa", "--email-file", emailFile, "--workers", "4", "--refresh-timeout", "60" };
            AddImportTargetArg(args, target);
            RunBackend("Impor Sekali Klik" + ImportTargetLabel(target) + " (" + rows.Count + ")", args);
        }

        private void ExportAccounts_Click(object sender, RoutedEventArgs e)
        {
            string format = ShowExportFormatDialog();
            if (format.Length == 0) return;

            var rows = ExportCandidateRows();
            if (format.Equals("txt", StringComparison.OrdinalIgnoreCase))
            {
                ExportAccountsTxt(rows);
                return;
            }
            if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
            {
                ExportAccountsJson(rows);
                return;
            }
            ExportAccountsConvertedJson(rows, format);
        }

        private List<PoolRow> ExportCandidateRows()
        {
            var rows = SelectedRowsOrCurrent();
            if (rows.Count == 0)
            {
                rows = allRows.Where(FilterRow).ToList();
            }
            if (rows.Count == 0)
            {
                rows = allRows.ToList();
            }
            return rows;
        }

        private void ExportAccountsTxt(List<PoolRow> rows)
        {
            var lines = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int skipped = 0;
            foreach (PoolRow row in rows)
            {
                if (TryBuildAccountExportLine(row, out string line))
                {
                    if (seen.Add(line))
                    {
                        lines.Add(line);
                    }
                }
                else
                {
                    skipped++;
                }
            }

            if (lines.Count == 0)
            {
                ShowThemedInfoDialog("Ekspor Sekali Klik", "Tidak menemukan rekaman email yang dapat diekspor. Hanya mendukung rekaman email yang berisi email, kata sandi, clientID, refresh token; rekaman CFWorker atau yang tidak memiliki kata sandi/refresh token akan dilewati.");
                return;
            }

            string outputDir = Path.Combine(rootDir, "runtime");
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, "account-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllLines(outputPath, lines, new UTF8Encoding(false));
            Log("One-click export wrote " + lines.Count + " account(s), skipped " + skipped + ": " + outputPath);
            ShowExportCompleteDialog(outputPath, lines.Count, skipped, "TXT", "Akun----Sandi----ID Klien----Token Refresh");
        }

        private void ExportAccountsJson(List<PoolRow> rows)
        {
            if (!TryCollectAccountExportJson(rows, out List<Dictionary<string, object>> items, out int skipped))
            {
                ShowThemedInfoDialog("Ekspor Sekali Klik", "Tidak menemukan rekaman akun JSON yang dapat diekspor. Membutuhkan rekaman akun yang telah memiliki session/auth_session atau rekaman SQLite asli.");
                return;
            }

            string outputDir = Path.Combine(rootDir, "runtime", "account_json");
            Directory.CreateDirectory(outputDir);
            string outputPath = Path.Combine(outputDir, "account-" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json");
            object payload = items.Count == 1 ? items[0] : items;
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, options), new UTF8Encoding(false));
            Log("One-click JSON export wrote " + items.Count + " account(s), skipped " + skipped + ": " + outputPath);
            ShowExportCompleteDialog(outputPath, items.Count, skipped, "JSON", "JSON session akun asli; pertahankan kolom RT, kosongkan default untuk akun tanpa RT");
        }

        private bool TryCollectAccountExportJson(List<PoolRow> rows, out List<Dictionary<string, object>> items, out int skipped)
        {
            items = new List<Dictionary<string, object>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            skipped = 0;
            foreach (PoolRow row in rows)
            {
                if (TryBuildAccountExportJson(row, out Dictionary<string, object> item))
                {
                    string key = JsonExportDedupKey(item, row);
                    if (seen.Add(key))
                    {
                        items.Add(item);
                    }
                }
                else
                {
                    skipped++;
                }
            }
            return items.Count > 0;
        }

        private void ExportAccountsConvertedJson(List<PoolRow> rows, string format)
        {
            if (!TryCollectAccountExportJson(rows, out List<Dictionary<string, object>> items, out int skipped))
            {
                ShowThemedInfoDialog("Ekspor Sekali Klik", "Tidak menemukan session akun yang dapat dikonversi. Membutuhkan akun yang telah memiliki access_token/session/auth_session atau rekaman SQLite asli.");
                return;
            }

            string normalized = (format ?? "cpa").Trim().ToLowerInvariant();
            string outputDir = Path.Combine(rootDir, "runtime", "account_json");
            Directory.CreateDirectory(outputDir);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string sourcePath = Path.Combine(Path.GetTempPath(), "account_export_source_" + stamp + ".json");
            string outputPath = Path.Combine(outputDir, "account-" + normalized + "-" + stamp + ".json");
            object payload = items.Count == 1 ? items[0] : items;
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(sourcePath, JsonSerializer.Serialize(payload, options), new UTF8Encoding(false));

            try
            {
                RunBackendWithResult("Ekspor akun konversi(" + normalized + ")", new List<string>
                {
                    "--convert-session-json", sourcePath,
                    "--convert-format", normalized,
                    "--convert-output", outputPath
                });
            }
            catch (Exception ex)
            {
                Log("Gagal konversi format akun:" + ex.Message);
                ShowThemedInfoDialog("Ekspor Sekali Klik", "Gagal konversi format akun:" + ex.Message);
                return;
            }
            finally
            {
                try { if (File.Exists(sourcePath)) File.Delete(sourcePath); } catch { }
            }

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
            {
                ShowThemedInfoDialog("Ekspor Sekali Klik", "Konversi format akun tidak menghasilkan file output, periksa log di bawah untuk memastikan hasil converter.");
                return;
            }

            Log("One-click converted export wrote " + items.Count + " account(s), skipped " + skipped + ", format=" + normalized + ": " + outputPath);
            ShowExportCompleteDialog(outputPath, items.Count, skipped, ExportFormatLabel(normalized), ExportFormatDescription(normalized));
        }

        private string ShowExportFormatDialog()
        {
            string selected = "";
            var dialog = new Window
            {
                Title = "Ekspor Sekali Klik",
                Owner = this,
                Width = 560,
                MinWidth = 520,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (Brush)FindResource("AppBg")
            };

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            header.Children.Add(new TextBlock
            {
                Text = "Pilih Format Ekspor",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextMain")
            });
            header.Children.Add(new TextBlock
            {
                Text = "TXT pertahankan format email asli; JSON asli mempertahankan session; format lain akan memanggil session_converter.py untuk dikonversi ke CPA/Sub2API/Cockpit/9router/Codex/AxonHub/Codex-Manager.",
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = (Brush)FindResource("TextSub")
            });
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var combo = new ComboBox { SelectedIndex = 2, Margin = new Thickness(0, 0, 0, 16) };
            combo.Items.Add(new ComboBoxItem { Content = "TXT - Email----Kata Sandi----ID Klien----Refresh Token", Tag = "txt" });
            combo.Items.Add(new ComboBoxItem { Content = "JSON Asli - session/auth_session", Tag = "json" });
            combo.Items.Add(new ComboBoxItem { Content = "CPA JSON", Tag = "cpa" });
            combo.Items.Add(new ComboBoxItem { Content = "Sub2API JSON", Tag = "sub2api" });
            combo.Items.Add(new ComboBoxItem { Content = "Cockpit JSON", Tag = "cockpit" });
            combo.Items.Add(new ComboBoxItem { Content = "9router JSON", Tag = "9router" });
            combo.Items.Add(new ComboBoxItem { Content = "Codex auth.json", Tag = "codex" });
            combo.Items.Add(new ComboBoxItem { Content = "AxonHub JSON", Tag = "axonhub" });
            combo.Items.Add(new ComboBoxItem { Content = "Codex-Manager JSON", Tag = "codexmanager" });
            Grid.SetRow(combo, 1);
            root.Children.Add(combo);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var exportButton = new Button
            {
                Content = "Ekspor",
                Width = 88,
                Style = (Style)FindResource("PrimaryButton")
            };
            exportButton.Click += (_, __) =>
            {
                selected = ((combo.SelectedItem as ComboBoxItem)?.Tag as string) ?? "cpa";
                dialog.Close();
            };
            var cancelButton = new Button
            {
                Content = "Batal",
                Width = 76,
                Margin = new Thickness(8, 0, 0, 0)
            };
            cancelButton.Click += (_, __) => dialog.Close();
            actions.Children.Add(exportButton);
            actions.Children.Add(cancelButton);
            Grid.SetRow(actions, 2);
            root.Children.Add(actions);

            dialog.Content = root;
            dialog.ShowDialog();
            return selected;
        }

        private string ExportFormatLabel(string format)
        {
            string value = (format ?? "").Trim().ToLowerInvariant();
            if (value == "sub2api") return "SUB2API JSON";
            if (value == "cockpit") return "Cockpit JSON";
            if (value == "9router") return "9router JSON";
            if (value == "codex") return "Codex auth.json";
            if (value == "axonhub") return "AxonHub JSON";
            if (value == "codexmanager") return "Codex-Manager JSON";
            if (value == "json") return "JSON Asli";
            if (value == "txt") return "TXT";
            return "CPA JSON";
        }

        private string ExportFormatDescription(string format)
        {
            string value = (format ?? "").Trim().ToLowerInvariant();
            if (value == "sub2api") return "Dokumen akun Sub2API yang dihasilkan oleh session_converter.py";
            if (value == "cockpit") return "Struktur impor Cockpit/Codex yang dihasilkan oleh session_converter.py";
            if (value == "9router") return "Struktur provider 9router yang dihasilkan oleh session_converter.py";
            if (value == "codex") return "Struktur auth.json Codex yang dihasilkan oleh session_converter.py";
            if (value == "axonhub") return "Struktur AxonHub yang dihasilkan oleh session_converter.py; placeholder akan ditulis jika RT tidak ada";
            if (value == "codexmanager") return "Struktur Codex-Manager yang dihasilkan oleh session_converter.py";
            return "Struktur JSON CPA yang dihasilkan oleh session_converter.py; bidang kompatibel akan disintesis jika id_token tidak ada";
        }

        private void ShowExportCompleteDialog(string outputPath, int exportedCount, int skippedCount, string formatLabel, string formatDescription)
        {
            var dialog = new Window
            {
                Title = "Ekspor Sekali Klik",
                Owner = this,
                Width = 520,
                MinWidth = 460,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (Brush)FindResource("AppBg")
            };

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            header.Children.Add(new TextBlock
            {
                Text = "Ekspor Selesai",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextMain")
            });
            header.Children.Add(new TextBlock
            {
                Text = "Akun telah dibuat " + formatLabel + " berkas:" + formatDescription,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = (Brush)FindResource("TextSub")
            });
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var summary = new Border
            {
                Background = (Brush)FindResource("PanelBg"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 16)
            };
            var summaryStack = new StackPanel();
            summaryStack.Children.Add(new TextBlock
            {
                Text = "Kuantitas:" + exportedCount + "    Lewati:" + skippedCount,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextMain")
            });
            summaryStack.Children.Add(new TextBlock
            {
                Text = outputPath,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                Foreground = (Brush)FindResource("TextSub")
            });
            summary.Child = summaryStack;
            Grid.SetRow(summary, 1);
            root.Children.Add(summary);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var openDirButton = new Button
            {
                Content = "Buka direktori",
                Width = 92,
                Style = (Style)FindResource("PrimaryButton")
            };
            openDirButton.Click += (_, __) =>
            {
                string directory = Path.GetDirectoryName(outputPath) ?? Path.Combine(rootDir, "runtime");
                OpenPath(directory);
                dialog.Close();
            };
            var closeButton = new Button
            {
                Content = "Tutup",
                Width = 76,
                Margin = new Thickness(8, 0, 0, 0)
            };
            closeButton.Click += (_, __) => dialog.Close();
            actions.Children.Add(openDirButton);
            actions.Children.Add(closeButton);
            Grid.SetRow(actions, 2);
            root.Children.Add(actions);

            dialog.Content = root;
            dialog.ShowDialog();
        }

        private void ShowAccountScanResultDialog(string backendOutput)
        {
            if (!TryExtractScanSummary(backendOutput, out Dictionary<string, object> summary))
            {
                ShowThemedInfoDialog("Tes Akun Aktif", "Tes Aktivasi Akun telah selesai, tetapi ringkasan hasil tidak berhasil diurai. Periksa log di bawah untuk detailnya.");
                return;
            }

            var results = new List<Dictionary<string, object>>();
            if (summary.TryGetValue("results", out object rawResults) && rawResults is List<object> items)
            {
                foreach (object item in items)
                {
                    if (item is Dictionary<string, object> map)
                    {
                        results.Add(map);
                    }
                }
            }

            bool directProbe = results.Any(r => TryGetMap(r, "probe", out Dictionary<string, object> _));
            var rtRows = directProbe ? new List<Dictionary<string, object>>() : results.Where(r => BoolValue(r, "has_rt")).ToList();
            var noRtRows = directProbe ? results : results.Where(r => !BoolValue(r, "has_rt")).ToList();

            var dialog = new Window
            {
                Title = "Hasil Tes Aktivasi Akun",
                Owner = this,
                Width = 600,
                MinWidth = 560,
                SizeToContent = SizeToContent.Height,
                MaxHeight = 760,
                ResizeMode = ResizeMode.CanResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (Brush)FindResource("AppBg")
            };

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            header.Children.Add(new TextBlock
            {
                Text = "Pengecekan aktivitas selesai",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextMain")
            });
            int directOk = results.Count(AccountLivenessProbeSucceeded);
            int direct401 = results.Count(AccountLivenessProbeReturned401);
            int directFailed = Math.Max(0, results.Count - directOk - direct401);
            header.Children.Add(new TextBlock
            {
                Text = directProbe
                    ? "Total:" + results.Count + "    AT valid:" + directOk + "    AT tidak valid:" + direct401 + "    Gagal lainnya:" + directFailed
                    : "Total:" + GetString(summary, "total")
                        + "    Normal:" + GetString(summary, "alive")
                        + "    Akun hilang:" + GetString(summary, "account_deactivated")
                        + "    401/AT tidak valid:" + GetString(summary, "at_invalid")
                        + "    Verifikasi SMS:" + GetString(summary, "secondary_phone_verification_required")
                        + "    Gagal:" + GetString(summary, "failed"),
                Margin = new Thickness(0, 6, 0, 0),
                Foreground = (Brush)FindResource("TextSub")
            });
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var body = new StackPanel();
            if (noRtRows.Count > 0)
            {
                AddScanResultSection(body, directProbe ? "AT Hasil Pemeriksaan" : "Hasil nomor belum ambil kode", noRtRows);
            }
            if (rtRows.Count > 0)
            {
                AddScanResultSection(body, "Hasil nomor yang telah diterima", rtRows);
            }
            if (body.Children.Count == 0)
            {
                body.Children.Add(new TextBlock
                {
                    Text = "Tidak ada detail pengecekan aktivitas yang dapat ditampilkan.",
                    Foreground = (Brush)FindResource("TextSub")
                });
            }

            var scroll = new ScrollViewer
            {
                Content = body,
                MaxHeight = 520,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            var ok = new Button { Content = "Tutup", Width = 82, Style = (Style)FindResource("PrimaryButton") };
            ok.Click += (_, __) => dialog.Close();
            actions.Children.Add(ok);
            Grid.SetRow(actions, 2);
            root.Children.Add(actions);

            dialog.Content = root;
            dialog.ShowDialog();
        }

        private void AddScanResultSection(StackPanel parent, string title, List<Dictionary<string, object>> rows)
        {
            parent.Children.Add(new TextBlock
            {
                Text = title + "（" + rows.Count + "）",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextMain"),
                Margin = new Thickness(0, parent.Children.Count == 0 ? 0 : 12, 0, 8)
            });

            var card = new Border
            {
                Background = (Brush)FindResource("PanelBg"),
                BorderBrush = (Brush)FindResource("Line"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10),
                Margin = new Thickness(0, 0, 0, 4)
            };
            var stack = new StackPanel();
            foreach (Dictionary<string, object> row in rows)
            {
                string email = GetString(row, "email");
                string status;
                string error;
                if (TryGetMap(row, "probe", out Dictionary<string, object> probe))
                {
                    status = AccountLivenessProbeStatusLabel(probe);
                    error = GetString(probe, "error");
                }
                else
                {
                    status = ScanStatusLabel(GetString(row, "scan_status"));
                    error = ScanResultError(row);
                }
                string line = error.Length > 0 ? email + "  ·  " + status + "  ·  " + error : email + "  ·  " + status;
                stack.Children.Add(new TextBlock
                {
                    Text = line,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 20,
                    Margin = new Thickness(0, 0, 0, 6),
                    Foreground = (Brush)FindResource("TextSub")
                });
            }
            card.Child = stack;
            parent.Children.Add(card);
        }

        private string ScanStatusLabel(string status)
        {
            string value = (status ?? "").Trim().ToLowerInvariant();
            return value switch
            {
                "alive" => "Normal",
                "alive_probe_inconclusive" => "RT normal / Deteksi mendalam OAuth belum selesai",
                "account_deactivated" => "Akun drop",
                "secondary_phone_verification_required" => "Verifikasi ponsel",
                "phone_verification_required" => "Pembayaran Selesai",
                "scan_failed" => "Pemindaian gagal",
                _ => value.Length > 0 ? value : "Tidak Dikenal"
            };
        }

        private bool AccountLivenessProbeSucceeded(Dictionary<string, object> row)
        {
            return TryGetMap(row, "probe", out Dictionary<string, object> probe) && BoolValue(probe, "ok");
        }

        private bool AccountLivenessProbeReturned401(Dictionary<string, object> row)
        {
            if (!TryGetMap(row, "probe", out Dictionary<string, object> probe)) return false;
            string status = GetString(probe, "status").Trim().ToLowerInvariant();
            return GetString(probe, "status_code") == "401" || status == "token_invalid";
        }

        private string AccountLivenessProbeStatusLabel(Dictionary<string, object> probe)
        {
            if (GetString(probe, "status_code") == "401" || GetString(probe, "status").Equals("token_invalid", StringComparison.OrdinalIgnoreCase))
            {
                return "AT Gagal / HTTP 401";
            }
            if (BoolValue(probe, "ok"))
            {
                string statusCode = GetString(probe, "status_code");
                return statusCode.Length > 0 ? "AT Aktif / HTTP " + statusCode : "AT masih berlaku";
            }
            string failedCode = GetString(probe, "status_code");
            return failedCode.Length > 0 ? "Pengecekan aktivitas gagal / HTTP " + failedCode : "Pengecekan aktivitas gagal";
        }

        private string ScanResultError(Dictionary<string, object> row)
        {
            foreach (string section in new[] { "oauth", "refresh" })
            {
                if (TryGetMap(row, section, out Dictionary<string, object> map))
                {
                    string error = GetString(map, "error");
                    if (error.Length > 0) return error;
                }
            }
            return "";
        }

        private bool TryExtractScanSummary(string output, out Dictionary<string, object> summary)
        {
            summary = null;
            string text = output ?? "";
            int end = text.LastIndexOf('}');
            if (end < 0) return false;
            for (int start = text.LastIndexOf('{', end); start >= 0; start = start > 0 ? text.LastIndexOf('{', start - 1) : -1)
            {
                string candidate = text.Substring(start, end - start + 1);
                try
                {
                    var parsed = JsonTextToObject(candidate);
                    if (parsed.ContainsKey("results") && parsed.ContainsKey("total"))
                    {
                        summary = parsed;
                        return true;
                    }
                }
                catch
                {
                }
            }
            return false;
        }

        private bool BoolValue(Dictionary<string, object> data, string key)
        {
            if (data == null || !data.TryGetValue(key, out object value) || value == null) return false;
            if (value is bool b) return b;
            string text = Convert.ToString(value)?.Trim() ?? "";
            return text.Equals("true", StringComparison.OrdinalIgnoreCase) || text == "1";
        }

        private bool TryBuildAccountExportJson(PoolRow row, out Dictionary<string, object> item)
        {
            item = null;
            if (row == null) return false;
            if (!TryLoadAccountDataForRow(row, out Dictionary<string, object> data) || data.Count == 0)
            {
                return false;
            }

            Dictionary<string, object> source = data;
            if (TryGetMap(data, "auth_session", out Dictionary<string, object> authSession) && authSession.Count > 0)
            {
                source = authSession;
            }

            if (CloneExportJsonValue(source) is not Dictionary<string, object> clean || clean.Count == 0)
            {
                return false;
            }

            EnsureJsonExportEmail(clean, row);
            EnsureJsonExportRefreshToken(clean, data);
            if (IsPayPalCompletedRow(row))
            {
                SetJsonExportPlanTypePlus(clean);
            }

            item = clean;
            return true;
        }

        private bool TryLoadAccountDataForRow(PoolRow row, out Dictionary<string, object> data)
        {
            data = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (row == null) return false;

            string source = (row.SourcePath ?? "").Trim();
                if (source.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase) && File.Exists(source))
                {
                    try
                    {
                        JsonElement account = desktopRead.ReadAccountExportAsync(
                            OnlyDigits(row.RawLine), row.Identifier).GetAwaiter().GetResult();
                        data = JsonElementToDictionary(account);
                        if (data.Count > 0) return true;
                    }
                    catch { }
                }

            var paths = new List<string> { row.Notes, row.SourcePath };
            foreach (string path in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!File.Exists(path) || !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    data = ReadJsonObject(path);
                    return data.Count > 0;
                }
                catch
                {
                }
            }
            return false;
        }



        private void MergeJsonObject(Dictionary<string, object> target, Dictionary<string, object> source)
        {
            if (target == null || source == null) return;
            foreach (var pair in source)
            {
                target[pair.Key] = pair.Value;
            }
        }

        private string SqlLiteral(string value)
        {
            return (value ?? "").Replace("'", "''");
        }

        private object CloneExportJsonValue(object value)
        {
            if (value is Dictionary<string, object> map)
            {
                var clean = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in map)
                {
                    clean[pair.Key] = CloneExportJsonValue(pair.Value);
                }
                return clean;
            }
            if (value is List<object> list)
            {
                return list.Select(CloneExportJsonValue).ToList();
            }
            return value;
        }

        private void EnsureJsonExportEmail(Dictionary<string, object> item, PoolRow row)
        {
            string email = (row?.Identifier ?? "").Trim();
            if (email.Length == 0) return;
            if (!TryGetMap(item, "user", out Dictionary<string, object> user))
            {
                user = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                item["user"] = user;
            }
            if (GetString(user, "email").Length == 0)
            {
                user["email"] = email;
            }
        }

        private void EnsureJsonExportRefreshToken(Dictionary<string, object> item, Dictionary<string, object> sourceData)
        {
            string rt = FirstJsonString(
                GetString(sourceData, "oauth_refresh_token"),
                GetString(sourceData, "refresh_token"),
                NestedJsonString(sourceData, "codex_session", "refresh_token"),
                NestedJsonString(sourceData, "token", "refresh_token"),
                NestedJsonString(sourceData, "credentials", "refresh_token")
            );
            item["refresh_token"] = rt;
            if (GetString(item, "oauth_refresh_token").Length == 0 && rt.Length > 0)
            {
                item["oauth_refresh_token"] = rt;
            }
        }

        private string NestedJsonString(Dictionary<string, object> data, string section, string key)
        {
            return TryGetMap(data, section, out Dictionary<string, object> map) ? GetString(map, key) : "";
        }

        private string FirstJsonString(params string[] values)
        {
            foreach (string value in values)
            {
                string text = (value ?? "").Trim();
                if (text.Length > 0) return text;
            }
            return "";
        }

        private void SetJsonExportPlanTypePlus(Dictionary<string, object> item)
        {
            if (item.ContainsKey("planType"))
            {
                item["planType"] = "plus";
            }
            if (!TryGetMap(item, "account", out Dictionary<string, object> account))
            {
                account = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                item["account"] = account;
            }
            account["planType"] = "plus";
        }

        private string JsonExportDedupKey(Dictionary<string, object> item, PoolRow row)
        {
            if (TryGetMap(item, "user", out Dictionary<string, object> user))
            {
                string userEmail = GetString(user, "email").Trim();
                if (userEmail.Length > 0) return userEmail.ToLowerInvariant();
            }
            string email = GetString(item, "email").Trim();
            if (email.Length > 0) return email.ToLowerInvariant();
            email = (row?.Identifier ?? "").Trim();
            if (email.Length > 0) return email.ToLowerInvariant();
            return JsonSerializer.Serialize(item);
        }

        private bool TryBuildAccountExportLine(PoolRow row, out string line)
        {
            line = "";
            if (row == null) return false;

            string source = FindMailboxLineForRow(row);
            if (source.Length == 0 && !string.IsNullOrWhiteSpace(row.RawLine))
            {
                source = row.RawLine;
            }

            if (!TryParseMailboxExportParts(source, row, out string email, out string password, out string clientId, out string refreshToken))
            {
                return false;
            }

            if (email.Length == 0 || password.Length == 0 || clientId.Length == 0 || refreshToken.Length == 0)
            {
                return false;
            }

            line = email + "----" + password + "----" + clientId + "----" + refreshToken;
            return true;
        }

        private bool TryParseMailboxExportParts(string source, PoolRow row, out string email, out string password, out string clientId, out string refreshToken)
        {
            email = "";
            password = "";
            clientId = "";
            refreshToken = "";

            string value = (source ?? "").Trim().TrimStart('\ufeff');
            if (value.Length == 0 || value.StartsWith("#")) return false;
            if (value.StartsWith("cfworker://", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith("@edu.liziai.cloud", StringComparison.OrdinalIgnoreCase)
                || value.EndsWith("@liziai.cloud", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (value.Contains("----"))
            {
                string[] parts = value.Split(new[] { "----" }, StringSplitOptions.None);
                if (parts.Length < 4) return false;
                email = parts[0].Trim();
                password = parts[1].Trim();
                string p2 = parts[2].Trim();
                string p3 = string.Join("----", parts.Skip(3)).Trim();
                clientId = LooksMicrosoftClientId(p2) || !LooksMicrosoftClientId(p3) ? p2 : p3;
                refreshToken = LooksMicrosoftClientId(p2) || !LooksMicrosoftClientId(p3) ? p3 : p2;
                return true;
            }

            if (value.Contains("---"))
            {
                string[] parts = value.Split(new[] { "---" }, StringSplitOptions.None);
                if (parts.Length < 3) return false;
                email = parts[0].Trim();
                password = parts[1].Trim();
                clientId = !string.IsNullOrWhiteSpace(row?.ClientId) ? row.ClientId.Trim() : DefaultMailboxClientId();
                refreshToken = parts[2].Trim();
                return true;
            }

            return false;
        }

        private bool LooksMicrosoftClientId(string value)
        {
            return Regex.IsMatch((value ?? "").Trim(), "^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");
        }

        private string DefaultMailboxClientId()
        {
            string configured = ConfigString("email_registration", "oauth_client_id").Trim();
            return configured.Length > 0 ? configured : "9e5f94bc-e8a4-4e73-b8be-63364c29d753";
        }

        private string ShowImportTargetDialog(string title)
        {
            string selected = "";
            var dialog = new Window
            {
                Title = title,
                Owner = this,
                Width = 360,
                Height = 190,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = (System.Windows.Media.Brush)FindResource("AppBg")
            };

            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Pilih Target Impor",
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(label, 0);
            root.Children.Add(label);

            var combo = new ComboBox { SelectedIndex = 0, Margin = new Thickness(0, 0, 0, 18) };
            combo.Items.Add(new ComboBoxItem { Content = "CPA", Tag = "cpa" });
            combo.Items.Add(new ComboBoxItem { Content = "SUB2API", Tag = "sub2api" });
            Grid.SetRow(combo, 1);
            root.Children.Add(combo);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            var ok = new Button { Content = "Konfirmasi", Width = 76, Style = (Style)FindResource("PrimaryButton") };
            ok.Click += (_, __) =>
            {
                selected = ((combo.SelectedItem as ComboBoxItem)?.Tag as string) ?? "cpa";
                dialog.Close();
            };
            var cancel = new Button { Content = "Batal", Width = 76, Margin = new Thickness(8, 0, 0, 0) };
            cancel.Click += (_, __) =>
            {
                selected = "";
                dialog.Close();
            };
            actions.Children.Add(ok);
            actions.Children.Add(cancel);
            Grid.SetRow(actions, 2);
            root.Children.Add(actions);

            dialog.Content = root;
            dialog.ShowDialog();
            return selected;
        }

        private void AddImportTargetArg(List<string> args, string target)
        {
            args.Add("--import-target");
            string value = (target ?? "").Trim().ToLowerInvariant();
            if (value == "sub2api")
            {
                args.Add("sub2api");
            }
            else if (value == "cliproxyapi")
            {
                args.Add("cliproxyapi");
            }
            else
            {
                args.Add("cpa");
            }
        }

        private string ImportTargetLabel(string target)
        {
            string value = (target ?? "").Trim().ToLowerInvariant();
            if (value == "sub2api") return "SUB2API";
            if (value == "cliproxyapi") return "CLIProxyAPI";
            return "CPA";
        }
    }
}
