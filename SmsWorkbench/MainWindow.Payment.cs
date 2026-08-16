namespace SmsWorkbench
{
    public partial class MainWindow
    {
        // Payment-link actions and unified protocol extractor
        private void OpenSessions_Click(object sender, RoutedEventArgs e) => OpenPath(GetSessionsDir());

        private void OpenDatabase_Click(object sender, RoutedEventArgs e) => OpenPath(GetDatabasePath());

        private void OpenMailboxPool_Click(object sender, RoutedEventArgs e) => OpenPath(GetMailboxTokenFile());

        private void OpenPayPalLink_Click(object sender, RoutedEventArgs e)
        {
            PoolRow row = SelectedEmailRowOrNotify("Buka Tautan Pembayaran");
            if (row == null) return;
            if (string.IsNullOrWhiteSpace(row.PayPalUrl))
            {
                MessageBox.Show("Akun yang dipilih tidak memiliki tautan pembayaran yang dapat dibuka.", "Tidak ada tautan pembayaran", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            OpenPayPalUrl(row.PayPalUrl, row.Identifier);
        }

        private void RegeneratePayPalLink_Click(object sender, RoutedEventArgs e)
        {
            var rows = SelectedEmailRowsOrNotify("Buat ulang tautan pembayaran");
            if (rows.Count == 0) return;
            string paymentMethod = ShowPaymentMethodDialog("Hasilkan ulang tautan", "Metode pembuatan tautan");
            if (paymentMethod.Length == 0) return;

            if (rows.Count == 1)
            {
                PoolRow row = rows[0];
                var singleArgs = new List<string> { "--email", row.Identifier, "--regenerate-paypal-link", "--workers", "4" };
                AddSessionFileArg(singleArgs, row);
                singleArgs.Add("--payment-method");
                singleArgs.Add(paymentMethod);
                RunBackend("Buat ulang tautan pembayaran", singleArgs);
                return;
            }

            string emailFile = Path.Combine(Path.GetTempPath(), "paypal_regen_emails_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllLines(emailFile, rows.Select(r => r.Identifier.Trim()), new UTF8Encoding(false));
            var args = new List<string> { "--regenerate-paypal-link", "--email-file", emailFile, "--workers", "4" };
            args.Add("--payment-method");
            args.Add(paymentMethod);
            RunBackend("Hasilkan ulang tautan pembayaran secara massal (" + rows.Count + ")", args);
        }

        private void MarkPayPalComplete_Click(object sender, RoutedEventArgs e)
        {
            var rows = SelectedEmailRowsOrNotify("Tandai Pembayaran Selesai");
            if (rows.Count == 0) return;
            MarkPayPalComplete(rows);
        }

        private void MarkPayPalComplete(PoolRow row)
        {
            MarkPayPalComplete(row == null ? new List<PoolRow>() : new List<PoolRow> { row });
        }

        private void MarkPayPalComplete(List<PoolRow> rows)
        {
            rows = (rows ?? new List<PoolRow>())
                .Where(r => !string.IsNullOrWhiteSpace(r.Identifier))
                .GroupBy(r => r.Identifier.Trim().ToLowerInvariant())
                .Select(g => g.First())
                .ToList();
            if (rows.Count == 0)
            {
                ShowEmailSelectionRequired("Tandai Pembayaran Selesai");
                return;
            }

            if (rows.Count == 1)
            {
                PoolRow row = rows[0];
                var singleArgs = new List<string> { "--email", row.Identifier, "--mark-paypal-status", "completed", "--workers", "4" };
                AddSessionFileArg(singleArgs, row);
                RunBackend("Tandai Pembayaran Selesai", singleArgs);
                return;
            }

            string emailFile = Path.Combine(Path.GetTempPath(), "paypal_completed_emails_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllLines(emailFile, rows.Select(r => r.Identifier.Trim()), new UTF8Encoding(false));
            var args = new List<string> { "--mark-paypal-status", "completed", "--email-file", emailFile, "--workers", "4" };
            RunBackend("Tandai pembayaran selesai secara massal (" + rows.Count + ")", args);
        }

        private void AtExtractBaLink_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedRowsOrCurrent()
                .Where(row => !string.IsNullOrWhiteSpace(row.Identifier))
                .GroupBy(row => row.Identifier.Trim().ToLowerInvariant())
                .Select(group => group.First())
                .ToList();
            if (selected.Count > 1)
            {
                ShowPaymentBatchDialog(selected);
                return;
            }
            ShowProtocolPaymentDialog(selected.FirstOrDefault());
        }

        /// <summary>
        /// Unified protocol payment-link extractor.
        /// </summary>
        private void ShowProtocolPaymentDialog(PoolRow selectedAccount = null)
        {
            ProtocolPaymentPreferences preferences = LoadProtocolPaymentPreferences();
            var win = new Window
            {
                Title = "Pembayaran Perjanjian",
                Width = 620,
                Height = 820,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.CanResize,
                Background = (System.Windows.Media.Brush)FindResource("AppBg"),
            };

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            };
            var mainPanel = new StackPanel { Margin = new Thickness(24) };

            // ── Judul ──────────────────────────────────────────────────────
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Pembayaran Perjanjian",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                Margin = new Thickness(0, 0, 0, 16),
            });

            if (selectedAccount != null)
            {
                mainPanel.Children.Add(new Border
                {
                    Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                    BorderBrush = (System.Windows.Media.Brush)FindResource("Line"),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 9, 12, 9),
                    Margin = new Thickness(0, 0, 0, 14),
                    CornerRadius = new CornerRadius(6),
                    Child = new TextBlock
                    {
                        Text = "Akun Terpilih: " + selectedAccount.Identifier + "\nSilakan pilih metode ekstraksi tautan pembayaran yang diinginkan.",
                        Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                        TextWrapping = TextWrapping.Wrap,
                    },
                });
            }

            // ── Pemilihan Metode Pembayaran ──────────────────────────────
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Metode pembayaran",
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSub"),
                Margin = new Thickness(0, 0, 0, 4),
            });
            var methodCombo = new ComboBox
            {
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 12),
            };
            foreach (PaymentMethodDefinition method in PaymentMethods.All)
            {
                methodCombo.Items.Add(new ComboBoxItem
                {
                    Content = method.SingleAccountDescription,
                    Tag = method.Id + "|" + method.DefaultCountry
                });
            }
            mainPanel.Children.Add(methodCombo);

            // ── Input AT ───────────────────────────────────────────────────
            var atLabel = new TextBlock
            {
                Text = "Access Token (JWT)",
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSub"),
                Margin = new Thickness(0, 0, 0, 4),
                Visibility = selectedAccount == null ? Visibility.Visible : Visibility.Collapsed,
            };
            mainPanel.Children.Add(atLabel);
            var atBox = new TextBox
            {
                Height = 80,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("Line"),
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = selectedAccount == null ? Visibility.Visible : Visibility.Collapsed,
            };
            mainPanel.Children.Add(atBox);

            // ── Negara Sasaran ──────────────────────────────────────────────────
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Negara penyelesaian (wilayah tagihan)",
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSub"),
                Margin = new Thickness(0, 0, 0, 4),
            });
            var countryCombo = new ComboBox
            {
                SelectedIndex = 0,
                Margin = new Thickness(0, 0, 0, 12),
            };
            var countries = new[] {
                "US - Amerika Serikat", "ID - Indonesia", "IN - India", "NL - Belanda",
                "BR - Brazil", "KR - Korea", "PL - Polandia", "CH - Swiss",
                "VN - Vietnam", "PH - Filipina",
                "DE - Jerman", "GB - Inggris", "JP - Jepang", "FR - Prancis",
                "AU - Australia", "SG - Singapura", "CA - Kanada", "NZ - Selandia Baru", "IE - Irlandia",
            };
            foreach (var c in countries)
                countryCombo.Items.Add(new ComboBoxItem { Content = c });
            mainPanel.Children.Add(countryCombo);

            // ── 代理配置 ──────────────────────────────────────────────────
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Alih Agen Tunggal (kosongkan untuk menggunakan kolam agen pembayaran perjanjian dari pengaturan)",
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSub"),
                Margin = new Thickness(0, 0, 0, 4),
            });
            var proxyBox = new TextBox
            {
                Text = preferences.Proxy,
                Height = 28,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("Line"),
                Margin = new Thickness(0, 0, 0, 4),
            };
            mainPanel.Children.Add(proxyBox);

            ComboBox CreateStageCountryCombo(string selectedCountry)
            {
                var combo = new ComboBox { MinWidth = 145 };
                foreach (var item in new[] {
                    ("US", "Amerika Serikat US"), ("GB", "Inggris GB"), ("DE", "Jerman DE"),
                    ("JP", "Jepang JP"), ("BR", "Brasil BR"), ("TR", "Turki TR"),
                    ("VN", "Vietnam VN"), ("ID", "Indonesia ID"), ("IN", "India IN"),
                    ("NL", "Belanda NL"), ("KR", "Korea Selatan KR"), ("PL", "Polandia PL"),
                    ("CH", "Swiss CH"), ("PH", "Filipina PH"),
                })
                {
                    combo.Items.Add(new ComboBoxItem { Content = item.Item2, Tag = item.Item1 });
                }
                string wanted = (selectedCountry ?? "").Trim().ToUpperInvariant();
                combo.SelectedIndex = 0;
                for (int index = 0; index < combo.Items.Count; index++)
                {
                    if (combo.Items[index] is ComboBoxItem option
                        && string.Equals(Convert.ToString(option.Tag), wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        combo.SelectedIndex = index;
                        break;
                    }
                }
                return combo;
            }

            var stageProxyPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 12) };
            stageProxyPanel.Children.Add(new TextBlock
            {
                Text = "Target Wilayah Agen Segmen",
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSub"),
                Margin = new Thickness(0, 0, 0, 5),
            });

            var stageGrid = new Grid();
            stageGrid.ColumnDefinitions.Add(new ColumnDefinition());
            stageGrid.ColumnDefinitions.Add(new ColumnDefinition());
            stageGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var checkoutCountryCombo = CreateStageCountryCombo(FirstNonEmpty(preferences.CheckoutCountry, "US"));
            var approveCountryCombo = CreateStageCountryCombo(FirstNonEmpty(preferences.ApproveCountry, "TR"));
            var updateCountryCombo = CreateStageCountryCombo(FirstNonEmpty(preferences.UpdateCountry, "TR"));
            var stageControls = new[]
            {
                ("Checkout", checkoutCountryCombo),
                ("Approve", approveCountryCombo),
                ("Update", updateCountryCombo),
            };
            for (int index = 0; index < stageControls.Length; index++)
            {
                var stageColumn = new StackPanel { Margin = new Thickness(index == 0 ? 0 : 5, 0, index == 2 ? 0 : 5, 0) };
                stageColumn.Children.Add(new TextBlock
                {
                    Text = stageControls[index].Item1,
                    FontSize = 11,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextSub"),
                    Margin = new Thickness(0, 0, 0, 3),
                });
                stageColumn.Children.Add(stageControls[index].Item2);
                Grid.SetColumn(stageColumn, index);
                stageGrid.Children.Add(stageColumn);
            }
            stageProxyPanel.Children.Add(stageGrid);
            mainPanel.Children.Add(stageProxyPanel);

            var blikCodePanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(0, 0, 0, 12) };
            blikCodePanel.Children.Add(new TextBlock
            {
                Text = "BLIK Enam Digit Kode",
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSub"),
                Margin = new Thickness(0, 0, 0, 4),
            });
            var blikCodeBox = new TextBox
            {
                MaxLength = 6,
                Height = 28,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("Line"),
            };
            blikCodePanel.Children.Add(blikCodeBox);
            mainPanel.Children.Add(blikCodePanel);

            // ── Opsi ──────────────────────────────────────────────────────
            var optionPanel = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 16) };
            var zeroCheck = new CheckBox
            {
                Content = "Wajib uji coba gratis / jumlah 0",
                IsChecked = true,
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                Margin = new Thickness(0, 0, 0, 6),
            };
            var requireBaCheck = new CheckBox
            {
                Content = "Harus mengembalikan URL otorisasi PayPal BA",
                IsChecked = true,
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                Margin = new Thickness(0, 0, 0, 0),
            };
            var jitRefreshCheck = new CheckBox
            {
                Content = "AT 401 Pulih Otomatis (RT/Cookie/Browser/OAuth)",
                IsChecked = true,
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                Margin = new Thickness(0, 0, 0, 6),
                Visibility = selectedAccount == null ? Visibility.Collapsed : Visibility.Visible,
            };
            var probeOnlyCheck = new CheckBox
            {
                Content = "Hanya deteksi kemampuan (Checkout + Stripe init)",
                IsChecked = false,
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                Margin = new Thickness(0, 0, 0, 6),
                Visibility = selectedAccount == null ? Visibility.Collapsed : Visibility.Visible,
            };
            optionPanel.Children.Add(jitRefreshCheck);
            optionPanel.Children.Add(probeOnlyCheck);
            optionPanel.Children.Add(zeroCheck);
            optionPanel.Children.Add(requireBaCheck);
            mainPanel.Children.Add(optionPanel);

            // ── Area Hasil ──────────────────────────────────────────────────
            mainPanel.Children.Add(new TextBlock
            {
                Text = "Hasil",
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSub"),
                Margin = new Thickness(0, 0, 0, 4),
            });
            var resultBox = new TextBox
            {
                Height = 120,
                TextWrapping = TextWrapping.Wrap,
                IsReadOnly = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                FontSize = 12,
                Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("Line"),
                Margin = new Thickness(0, 0, 0, 12),
            };
            mainPanel.Children.Add(resultBox);

            // ── Panel Tombol ──────────────────────────────────────────────────
            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var extractBtn = new Button
            {
                Content = "Ekstraksi",
                Height = 32,
                MinWidth = 100,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 8, 0),
            };
            var testProxyBtn = new Button
            {
                Content = "Uji ekspor",
                Height = 32,
                MinWidth = 88,
                Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("Line"),
                Margin = new Thickness(0, 0, 8, 0),
            };
            var copyBtn = new Button
            {
                Content = "Salin tautan",
                Height = 32,
                MinWidth = 80,
                IsEnabled = false,
                Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("Line"),
                Margin = new Thickness(0, 0, 8, 0),
            };
            var openQrBtn = new Button
            {
                Content = "Buka kode QR",
                Height = 32,
                MinWidth = 80,
                IsEnabled = false,
                Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("Line"),
                Margin = new Thickness(0, 0, 8, 0),
            };
            var cancelBtn = new Button
            {
                Content = "Batal",
                Height = 32,
                MinWidth = 60,
                IsEnabled = false,
                Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("Line"),
                Margin = new Thickness(0, 0, 8, 0),
            };
            var closeBtn = new Button
            {
                Content = "Tutup",
                Height = 32,
                MinWidth = 60,
                Background = (System.Windows.Media.Brush)FindResource("PanelBg"),
                Foreground = (System.Windows.Media.Brush)FindResource("TextMain"),
                BorderBrush = (System.Windows.Media.Brush)FindResource("Line"),
            };
            btnPanel.Children.Add(testProxyBtn);
            btnPanel.Children.Add(extractBtn);
            btnPanel.Children.Add(copyBtn);
            btnPanel.Children.Add(openQrBtn);
            btnPanel.Children.Add(cancelBtn);
            btnPanel.Children.Add(closeBtn);
            mainPanel.Children.Add(btnPanel);

            scrollViewer.Content = mainPanel;
            win.Content = scrollViewer;

            string lastUrl = "";
            string lastQrPath = "";
            CancellationTokenSource executionCancellation = null;
            bool closeAfterCancellation = false;

            string SelectedMethod()
            {
                if (methodCombo.SelectedItem is not ComboBoxItem item) return "paypal";
                string tag = Convert.ToString(item.Tag) ?? "paypal|US";
                return tag.Split('|')[0];
            }

            void UpdateActionButton()
            {
                if (probeOnlyCheck.IsChecked == true)
                {
                    extractBtn.Content = "Mulai Deteksi";
                    return;
                }
                string method = SelectedMethod();
                if (method == "blik")
                {
                    extractBtn.Content = "Eksekusi pembayaran";
                    return;
                }
                extractBtn.Content = "Ekstraksi";
            }

            string ComboCode(ComboBox combo)
            {
                return combo.SelectedItem is ComboBoxItem item
                    ? (Convert.ToString(item.Tag) ?? "").Trim().ToUpperInvariant()
                    : "";
            }

            void SelectComboCode(ComboBox combo, string country)
            {
                for (int index = 0; index < combo.Items.Count; index++)
                {
                    if (combo.Items[index] is ComboBoxItem item
                        && string.Equals(Convert.ToString(item.Tag), country, StringComparison.OrdinalIgnoreCase))
                    {
                        combo.SelectedIndex = index;
                        return;
                    }
                }
            }

            void SaveSelection()
            {
                SaveProtocolPaymentPreferences(new ProtocolPaymentPreferences
                {
                    Method = SelectedMethod(),
                    Proxy = proxyBox.Text.Trim(),
                    TargetCountry = countryCombo.SelectedItem is ComboBoxItem targetItem
                        ? (Convert.ToString(targetItem.Content) ?? "").Substring(0, 2)
                        : "US",
                    CheckoutCountry = ComboCode(checkoutCountryCombo),
                    ApproveCountry = ComboCode(approveCountryCombo),
                    UpdateCountry = ComboCode(updateCountryCombo),
                });
            }

            // ── 支付方式切换时更新国家默认值 ──────────────────────────────
            // ── Perbarui nilai default negara saat metode pembayaran diubah ──────────────────────────────
            methodCombo.SelectionChanged += (_, __) =>
            {
                string method = SelectedMethod();
                string tag = Convert.ToString((methodCombo.SelectedItem as ComboBoxItem)?.Tag) ?? "paypal|US";
                string[] tagParts = tag.Split('|');
                string defaultCountry = tagParts.Length > 1 ? tagParts[1] : "US";
                for (int index = 0; index < countryCombo.Items.Count; index++)
                {
                    if (countryCombo.Items[index] is ComboBoxItem countryItem && Convert.ToString(countryItem.Content)?.StartsWith(defaultCountry + " ", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        countryCombo.SelectedIndex = index;
                        break;
                    }
                }
                if (method != "paypal")
                {
                    SelectComboCode(checkoutCountryCombo, defaultCountry);
                    SelectComboCode(approveCountryCombo, defaultCountry);
                    SelectComboCode(updateCountryCombo, defaultCountry);
                }
                requireBaCheck.IsEnabled = method == "paypal";
                blikCodePanel.Visibility = method == "blik" ? Visibility.Visible : Visibility.Collapsed;
                stageProxyPanel.Visibility = method == "paypal" || method == "gopay" || method == "gcash" || method == "grabpay" || method == "upi" || method == "direct_card" || method == "momo" ? Visibility.Visible : Visibility.Collapsed;
                updateCountryCombo.IsEnabled = method == "paypal" || method == "direct_card";
                zeroCheck.IsChecked = true;
                zeroCheck.IsEnabled = probeOnlyCheck.IsChecked != true;
                UpdateActionButton();
            };
            probeOnlyCheck.Checked += (_, __) =>
            {
                zeroCheck.IsEnabled = false;
                requireBaCheck.IsEnabled = false;
                UpdateActionButton();
            };
            probeOnlyCheck.Unchecked += (_, __) =>
            {
                zeroCheck.IsEnabled = true;
                requireBaCheck.IsEnabled = SelectedMethod() == "paypal";
                UpdateActionButton();
            };
            for (int index = 0; index < methodCombo.Items.Count; index++)
            {
                if (methodCombo.Items[index] is ComboBoxItem item
                    && string.Equals(Convert.ToString(item.Tag)?.Split('|')[0], preferences.Method, StringComparison.OrdinalIgnoreCase))
                {
                    methodCombo.SelectedIndex = index;
                    break;
                }
            }
            if (!string.IsNullOrWhiteSpace(preferences.TargetCountry))
            {
                for (int index = 0; index < countryCombo.Items.Count; index++)
                {
                    if (countryCombo.Items[index] is ComboBoxItem item
                        && Convert.ToString(item.Content)?.StartsWith(preferences.TargetCountry + " ", StringComparison.OrdinalIgnoreCase) == true)
                    {
                        countryCombo.SelectedIndex = index;
                        break;
                    }
                }
            }

            testProxyBtn.Click += async (_, __) =>
            {
                SaveSelection();
                var args = ProtocolPaymentExecutionPlanner.CreateProxyTestArguments(
                    SelectedMethod(),
                    proxyBox.Text,
                    ComboCode(checkoutCountryCombo),
                    ComboCode(approveCountryCombo),
                    ComboCode(updateCountryCombo)).ToList();

                resultBox.Text = "Menguji ekspor proxy checkout / approve / update...";
                testProxyBtn.IsEnabled = false;
                extractBtn.IsEnabled = false;
                try
                {
                    string result = await Task.Run(() => RunBackendWithResult("Uji proxy pembayaran protokol", args));
                    using JsonDocument json = JsonDocument.Parse(result);
                    JsonElement root = json.RootElement;
                    var lines = new List<string>();
                    bool allOk = root.TryGetProperty("ok", out JsonElement okEl) && okEl.GetBoolean();
                    lines.Add(allOk ? "[Sukses] Keluar proxy sesuai pilihan" : "[Gagal] Terdapat proxy yang tidak tersedia atau tidak cocok wilayahnya");
                    if (root.TryGetProperty("stages", out JsonElement stagesEl) && stagesEl.ValueKind == JsonValueKind.Object)
                    {
                        foreach (string stage in new[] { "checkout", "approve", "update" })
                        {
                            if (!stagesEl.TryGetProperty(stage, out JsonElement stageEl)) continue;
                            string ip = stageEl.TryGetProperty("ip", out JsonElement ipEl) ? ipEl.GetString() ?? "" : "";
                            string actual = stageEl.TryGetProperty("country_code", out JsonElement ccEl) ? ccEl.GetString() ?? "" : "";
                            string expected = stageEl.TryGetProperty("expected_country", out JsonElement expectedEl) ? expectedEl.GetString() ?? "" : "";
                            string error = stageEl.TryGetProperty("error", out JsonElement errorEl) ? errorEl.GetString() ?? "" : "";
                            lines.Add($"{stage}: {ip} / {actual} (target {expected})" + (error.Length > 0 ? $" - {error}" : ""));
                        }
                    }
                    resultBox.Text = string.Join(Environment.NewLine, lines);
                }
                catch (Exception ex)
                {
                    resultBox.Text = "[Pengecualian] " + ex.Message;
                }
                finally
                {
                    testProxyBtn.IsEnabled = true;
                    extractBtn.IsEnabled = true;
                }
            };

            // ── Tombol Ekstrak ──────────────────────────────────────────────────
            extractBtn.Click += async (_, __) =>
            {
                string at = atBox.Text.Trim();
                if (selectedAccount == null && string.IsNullOrEmpty(at))
                {
                    resultBox.Text = "Masukkan Access Token";
                    return;
                }

                string method = SelectedMethod();
                if (probeOnlyCheck.IsChecked != true
                    && method == "blik"
                    && (blikCodeBox.Text.Trim().Length != 6 || !blikCodeBox.Text.Trim().All(char.IsDigit)))
                {
                    resultBox.Text = "Masukkan kode BLIK 6 digit yang valid.";
                    return;
                }
                string country = "US";
                if (countryCombo.SelectedItem is ComboBoxItem ci && ci.Content.ToString().Length >= 2)
                    country = ci.Content.ToString().Substring(0, 2);

                string proxy = proxyBox.Text.Trim();
                bool requireZero = zeroCheck.IsChecked == true;
                bool requireBaToken = requireBaCheck.IsChecked == true;
                SaveSelection();

                extractBtn.IsEnabled = false;
                testProxyBtn.IsEnabled = false;
                cancelBtn.IsEnabled = true;
                copyBtn.IsEnabled = false;
                openQrBtn.IsEnabled = false;
                string transientSessionFile = "";
                using var cancellation = new CancellationTokenSource();
                executionCancellation = cancellation;
                ProtocolPaymentExecutionPlan plan = null;

                try
                {
                    string sessionFile;
                    if (selectedAccount == null)
                    {
                        transientSessionFile = Path.Combine(Path.GetTempPath(), "protocol_payment_at_" + Guid.NewGuid().ToString("N") + ".json");
                        File.WriteAllText(
                            transientSessionFile,
                            JsonSerializer.Serialize(new Dictionary<string, string> { ["access_token"] = at }),
                            new UTF8Encoding(false));
                        sessionFile = transientSessionFile;
                    }
                    else
                    {
                        sessionFile = SessionFileFor(selectedAccount);
                    }

                    plan = ProtocolPaymentExecutionPlanner.Create(
                        new ProtocolPaymentExecutionRequest(
                            method,
                            country,
                            proxy,
                            jitRefreshCheck.IsChecked == true,
                            probeOnlyCheck.IsChecked == true,
                            requireZero,
                            requireBaToken,
                            blikCodeBox.Text,
                            ComboCode(checkoutCountryCombo),
                            ComboCode(approveCountryCombo),
                            ComboCode(updateCountryCombo),
                            selectedAccount?.Identifier ?? "",
                            sessionFile));
                    var args = plan.Arguments.ToList();
                    resultBox.Text = plan.StatusText;
                    int timeoutMs = ProtocolPaymentBackendTimeoutMs(method);
                    Log("Mulai: python " + FormatBackendArgsForDisplay(args));
                    BackendCommandResult backendResult = await backendClient.RunAsync(
                        BackendCommand.Create(plan.TaskName, args, timeoutMs),
                        cancellationToken: cancellation.Token);
                    string result;
                    if (backendResult.Payload.HasValue)
                        result = backendResult.Payload.Value.GetRawText();
                    else if (backendResult.TimedOut)
                    {
                        ProtocolPaymentResultPresentation timedOut = ProtocolPaymentResultPresenter.Aborted(plan, "timed_out");
                        resultBox.Text = timedOut.Text;
                        lastUrl = timedOut.Url;
                        lastQrPath = timedOut.QrPath;
                        copyBtn.IsEnabled = false;
                        openQrBtn.IsEnabled = false;
                        return;
                    }
                    else if (!string.IsNullOrWhiteSpace(backendResult.StandardError))
                        throw new InvalidOperationException(backendResult.StandardError);
                    else
                        result = backendResult.StandardOutput;

                    ProtocolPaymentResultPresentation presentation = ProtocolPaymentResultPresenter.Parse(result);
                    resultBox.Text = presentation.Text;
                    lastUrl = presentation.Url;
                    lastQrPath = presentation.QrPath;
                    copyBtn.IsEnabled = lastUrl.Length > 0;
                    openQrBtn.IsEnabled = lastQrPath.Length > 0 && File.Exists(lastQrPath);
                }
                catch (OperationCanceledException)
                {
                    ProtocolPaymentResultPresentation cancelled = ProtocolPaymentResultPresenter.Aborted(plan, "cancelled");
                    resultBox.Text = cancelled.Text;
                    lastUrl = cancelled.Url;
                    lastQrPath = cancelled.QrPath;
                    copyBtn.IsEnabled = false;
                    openQrBtn.IsEnabled = false;
                }
                catch (TimeoutException)
                {
                    ProtocolPaymentResultPresentation timedOut = ProtocolPaymentResultPresenter.Aborted(plan, "timed_out");
                    resultBox.Text = timedOut.Text;
                    lastUrl = timedOut.Url;
                    lastQrPath = timedOut.QrPath;
                    copyBtn.IsEnabled = false;
                    openQrBtn.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    resultBox.Text = $"[Pengecualian] {ex.Message}";
                }
                finally
                {
                    try
                    {
                        if (transientSessionFile.Length > 0)
                            File.Delete(transientSessionFile);
                    }
                    catch { }
                    if (ReferenceEquals(executionCancellation, cancellation))
                        executionCancellation = null;
                    extractBtn.IsEnabled = true;
                    testProxyBtn.IsEnabled = true;
                    cancelBtn.IsEnabled = false;
                    if (closeAfterCancellation)
                        win.Close();
                }
            };

            // ── Tombol Salin ──────────────────────────────────────────────────
            copyBtn.Click += (_, __) =>
            {
                if (!string.IsNullOrEmpty(lastUrl))
                {
                    System.Windows.Clipboard.SetText(lastUrl);
                    copyBtn.Content = "Telah Disalin!";
                    Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => copyBtn.Content = "Salin tautan"));
                }
            };

            // ── Tombol Buka QR ─────────────────────────────────────────────
            openQrBtn.Click += (_, __) =>
            {
                if (!string.IsNullOrEmpty(lastQrPath) && File.Exists(lastQrPath))
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = lastQrPath,
                            UseShellExecute = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Gagal membuka gambar QR: {ex.Message}", "Kesalahan", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            };

            cancelBtn.Click += (_, __) =>
            {
                if (executionCancellation == null) return;
                resultBox.Text = "Membatalkan tugas pembayaran perjanjian...";
                cancelBtn.IsEnabled = false;
                executionCancellation.Cancel();
            };

            closeBtn.Click += (_, __) =>
            {
                SaveSelection();
                win.Close();
            };
            win.Closing += (_, args) =>
            {
                if (executionCancellation == null) return;
                args.Cancel = true;
                closeAfterCancellation = true;
                resultBox.Text = "Membatalkan tugas pembayaran perjanjian...";
                cancelBtn.IsEnabled = false;
                executionCancellation.Cancel();
            };
            win.Closed += (_, __) => SaveSelection();

            win.ShowDialog();
        }

        private ProtocolPaymentPreferences LoadProtocolPaymentPreferences()
        {
            string path = ProtocolPaymentPreferencesPath();
            try
            {
                if (File.Exists(path))
                {
                    ProtocolPaymentHistoryFile saved = JsonSerializer.Deserialize<ProtocolPaymentHistoryFile>(
                        File.ReadAllText(path, Encoding.UTF8),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (saved?.Last != null)
                    {
                        if (RemoveProtocolPaymentSecrets(saved))
                            File.WriteAllText(path, JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
                        return saved.Last;
                    }
                }
            }
            catch (Exception ex)
            {
                Log("Gagal membaca pemilihan riwayat pembayaran protokol: " + ex.Message);
            }

            var defaults = new ProtocolPaymentPreferences();
            try
            {
                Dictionary<string, object> config = ReadJsonObject(Path.Combine(rootDir, "config.json"));
                Dictionary<string, object> paypal = GetSection(config, "paypal");
                Dictionary<string, object> countries = GetSection(paypal, "stage_proxy_countries");
                defaults.CheckoutCountry = FirstNonEmpty(GetString(countries, "checkout"), "US");
                defaults.ApproveCountry = FirstNonEmpty(GetString(countries, "approve"), "TR");
                defaults.UpdateCountry = FirstNonEmpty(GetString(countries, "promotion"), "TR");
                defaults.TargetCountry = FirstNonEmpty(GetString(paypal, "target_country"), "US");
            }
            catch
            {
            }
            return defaults;
        }

        private void SaveProtocolPaymentPreferences(ProtocolPaymentPreferences preferences)
        {
            if (preferences == null) return;
            string path = ProtocolPaymentPreferencesPath();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? rootDir);
                ProtocolPaymentHistoryFile saved = null;
                if (File.Exists(path))
                {
                    try
                    {
                        saved = JsonSerializer.Deserialize<ProtocolPaymentHistoryFile>(
                            File.ReadAllText(path, Encoding.UTF8),
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch
                    {
                    }
                }
                saved ??= new ProtocolPaymentHistoryFile();
                saved.History ??= new List<ProtocolPaymentHistoryEntry>();
                preferences.Proxy = "";
                RemoveProtocolPaymentSecrets(saved);
                string signature = preferences.Signature();
                if (saved.History.Count == 0 || !string.Equals(saved.History[0].Signature, signature, StringComparison.Ordinal))
                {
                    saved.History.Insert(0, new ProtocolPaymentHistoryEntry
                    {
                        SavedAt = DateTimeOffset.Now.ToString("O"),
                        Signature = signature,
                        Selection = preferences,
                    });
                }
                saved.History = saved.History.Take(20).ToList();
                saved.Last = preferences;
                File.WriteAllText(path, JsonSerializer.Serialize(saved, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Log("Gagal menyimpan pemilihan riwayat pembayaran perjanjian:" + ex.Message);
            }
        }

        private string ProtocolPaymentPreferencesPath()
        {
            return Path.Combine(rootDir, "runtime", "protocol_payment_history.json");
        }

        private bool RemoveProtocolPaymentSecrets(ProtocolPaymentHistoryFile saved)
        {
            bool changed = false;
            void ClearProxy(ProtocolPaymentPreferences selection)
            {
                if (selection == null || string.IsNullOrEmpty(selection.Proxy)) return;
                selection.Proxy = "";
                changed = true;
            }

            ClearProxy(saved?.Last);
            foreach (ProtocolPaymentHistoryEntry entry in saved?.History ?? new List<ProtocolPaymentHistoryEntry>())
            {
                ClearProxy(entry?.Selection);
                if (entry?.Selection != null)
                    entry.Signature = entry.Selection.Signature();
            }
            return changed;
        }

        private int ProtocolPaymentBackendTimeoutMs(string paymentMethod)
        {
            int seconds = 900;
            try
            {
                Dictionary<string, object> config = ReadJsonObject(Path.Combine(rootDir, "config.json"));
                Dictionary<string, object> protocol = GetSection(config, "protocol_payments");
                if (int.TryParse(GetString(protocol, "timeout_seconds"), out int configured))
                    seconds = configured;
                Dictionary<string, object> methods = GetChildSection(protocol, "methods");
                Dictionary<string, object> method = GetChildSection(methods, NormalizePaymentMethod(paymentMethod));
                if (int.TryParse(GetString(method, "timeout_seconds"), out int methodConfigured))
                    seconds = methodConfigured;
            }
            catch { }
            seconds = Math.Max(30, Math.Min(3600, seconds));
            return (seconds + 30) * 1000;
        }

        private sealed class ProtocolPaymentPreferences
        {
            public string Method { get; set; } = "paypal";
            public string Proxy { get; set; } = "";
            public string TargetCountry { get; set; } = "US";
            public string CheckoutCountry { get; set; } = "US";
            public string ApproveCountry { get; set; } = "TR";
            public string UpdateCountry { get; set; } = "TR";

            public string Signature()
            {
                return string.Join("|", Method, TargetCountry, CheckoutCountry, ApproveCountry, UpdateCountry);
            }
        }

        private sealed class ProtocolPaymentHistoryEntry
        {
            public string SavedAt { get; set; } = "";
            public string Signature { get; set; } = "";
            public ProtocolPaymentPreferences Selection { get; set; } = new ProtocolPaymentPreferences();
        }

        private sealed class ProtocolPaymentHistoryFile
        {
            public ProtocolPaymentPreferences Last { get; set; } = new ProtocolPaymentPreferences();
            public List<ProtocolPaymentHistoryEntry> History { get; set; } = new List<ProtocolPaymentHistoryEntry>();
        }

    }
}
