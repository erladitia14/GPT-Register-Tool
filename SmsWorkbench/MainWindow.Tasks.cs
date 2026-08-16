namespace SmsWorkbench
{
    public partial class MainWindow
    {
        // Backend process, task list, deletion and cancellation actions
        private void RerunFailed_Click(object sender, RoutedEventArgs e)
        {
            var failedRows = allRows.Where(r =>
                (r.Status.Contains("Gagal") || r.Status.Contains("Menunggu") || r.Status.Contains("tidak ada", StringComparison.OrdinalIgnoreCase))
                && IsMailboxPoolLikeRow(r)
                && !string.IsNullOrWhiteSpace(r.RawLine)).ToList();

            if (failedRows.Count == 0)
            {
                MessageBox.Show("Tidak menemukan akun gagal yang perlu registrasi ulang.", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Ditemukan {failedRows.Count} akun gagal/menunggu, konfirmasi untuk registrasi ulang?\n\nAlur: registrasi→ambil token akses→simpan session ke database",
                "Konfirmasi registrasi ulang", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            if (!TryCreateMailboxFile(failedRows, out string mailboxArg, out string tempFile, out int mailboxCount))
            {
                MessageBox.Show("Catatan kegagalan tidak memiliki kredensial email yang tersedia, tidak dapat mendaftar ulang.", "Format tidak cocok", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var args = new List<string> { mailboxArg, tempFile, "--count", mailboxCount.ToString(CultureInfo.InvariantCulture), "--workers", "4" };
            AddNoPhoneRegistrationArgs(args);
            AddRegistrationProxy(args);
            RunBackend("Daftarkan ulang akun yang gagal (" + mailboxCount + ")", args);
        }

        private void RebuildSqlite_Click(object sender, RoutedEventArgs e)
        {
            var args = new List<string> { "--rebuild-sqlite" };
            RunBackend("Bangun Ulang Indeks SQLite", args);
        }

        private void AccountGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            foreach (object item in e.AddedItems)
            {
                if (item is PoolRow row) row.IsChecked = true;
            }
        }

        private void AccountDetail_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PoolRow row)
            {
                ShowAccountDetail(row);
            }
        }

        private async void RunBackend(string taskName, List<string> args)
        {
            if (backendTasks.IsRunning)
            {
                MessageBox.Show("Sudah ada batch berjalan, batalkan atau tunggu selesai.", "Berjalan", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string safeArgs = FormatBackendArgsForDisplay(args);
            var task = new TaskRow { Name = "Batch " + taskSeq++, Task = taskName, Status = "Berjalan", Info = safeArgs };
            Tasks.Add(task);
            ScrollTaskGridToBottom();
            DateTime started = DateTime.Now;

            var backendOutput = new StringBuilder();
            object backendOutputLock = new object();
            void CaptureBackendLine(string line)
            {
                lock (backendOutputLock)
                {
                    backendOutput.AppendLine(line);
                }
            }

            var progress = new Progress<BackendOutputLine>(line =>
            {
                CaptureBackendLine(line.Text);
                UiLog(line.Text);
            });
            try
            {
                Log("Mulai: python " + safeArgs);
                StatusText = taskName + "  Berjalan";
                BackendCommandResult result = await backendTasks.RunAsync(
                    BackendCommand.Create(taskName, args, 12 * 60 * 60 * 1000),
                    progress);

                task.Status = result.ExitCode == 0 ? "Selesai" : "Gagal";
                task.Cost = ((int)(DateTime.Now - started).TotalSeconds).ToString(CultureInfo.InvariantCulture);
                task.DoneAt = SafeTime(DateTime.Now);
                StatusText = taskName + " selesai";
                RefreshPools();
                ScrollTaskGridToBottom();
                if (taskName.StartsWith("Tes Akun Aktif", StringComparison.OrdinalIgnoreCase))
                {
                    string output;
                    lock (backendOutputLock)
                    {
                        output = backendOutput.ToString();
                    }
                    ShowAccountScanResultDialog(output);
                }
            }
            catch (OperationCanceledException)
            {
                task.Status = "Dibatalkan";
                task.DoneAt = SafeTime(DateTime.Now);
                StatusText = taskName + " dibatalkan";
            }
            catch (BackendTaskAlreadyRunningException)
            {
                task.Status = "Belum dimulai";
                task.DoneAt = SafeTime(DateTime.Now);
                StatusText = taskName + " belum dimulai";
                MessageBox.Show("Sudah ada batch berjalan, batalkan atau tunggu selesai.", "Berjalan", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                task.Status = "Gagal Memulai";
                Log("Gagal memulai:" + ex.Message);
            }
        }

        private string RunBackendWithResult(string taskName, List<string> args, int timeoutMs = 120000)
        {
            Log("Mulai: python " + FormatBackendArgsForDisplay(args));
            return backendTasks.RunForResultAsync(
                BackendCommand.Create(taskName, args, timeoutMs)).GetAwaiter().GetResult();
        }

        private static string FormatBackendArgsForDisplay(List<string> args)
        {
            return SensitiveDataSanitizer.RedactArguments(args);
        }

        private void TaskGrid_Loaded(object sender, RoutedEventArgs e) => ScrollTaskGridToBottom();

        private void ScrollTaskGridToBottom()
        {
            if (TaskGrid == null || Tasks.Count == 0) return;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                object last = Tasks[Tasks.Count - 1];
                TaskGrid.SelectedItem = last;
                TaskGrid.ScrollIntoView(last);
            }), DispatcherPriority.Background);
        }

        private async void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = SelectedEmailRowsOrNotify("Hapus");
            if (selected.Count == 0) return;
            if (!await ShowDeleteConfirmDialog(selected.Count)) return;
            int failed = 0;
            foreach (PoolRow row in selected)
            {
                if (!await DeleteRowAsync(row)) failed++;
            }
            RefreshPools();
            if (failed > 0)
            {
                await DialogFactory.ShowInfoAsync(
                    this,
                    "Hapus Belum Selesai",
                    failed + " catatan tidak dapat dihapus sepenuhnya. Periksa log proses.");
            }
        }

        private async Task<bool> ShowDeleteConfirmDialog(int count)
        {
            return await DialogFactory.ShowConfirmAsync(
                this,
                "Hapus yang dipilih " + count + " catatan?",
                "Akan membersihkan secara sinkron kolam email lokal, indeks SQLite, dan file session yang cocok. Tindakan ini tidak dapat dibatalkan.",
                "Hapus",
                isDanger: true);
        }

        private async Task<bool> DeleteRowAsync(PoolRow row)
        {
            try
            {
                string emailKey = NormalizeEmailKey(row.Identifier);
                if (emailKey.Length == 0) return false;
                var args = new List<string> { "--delete-account", "--email", emailKey, "--desktop-ipc" };
                BackendCommandResult backend = await backendTasks.RunAsync(
                    BackendCommand.Create("Hapus Akun", args, 120000));
                if (backend.ExitCode != 0 || !backend.Payload.HasValue)
                {
                    Log("Gagal menghapus:" + SensitiveDataSanitizer.Redact(emailKey));
                    return false;
                }
                Log("Hapus akun selesai:" + SensitiveDataSanitizer.Redact(emailKey));
                return true;
#if LEGACY_DELETE_CODE
#pragma warning disable CS0162
                string legacyEmailKey = NormalizeEmailKey(row.Identifier);
                int removedPoolLines = DeleteMailboxLines(row, emailKey);
                int removedSqliteRows = DeleteSqliteAccountRows(row, emailKey);
                int removedSessionFiles = DeleteSessionJsonFiles(row, emailKey);

                if (row.SourcePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(row.SourcePath)
                    && IsUnderDirectory(row.SourcePath, GetSessionsDir()))
                {
                    File.Delete(row.SourcePath);
                    removedSessionFiles++;
                }

                Log("Hapus akun:" + row.Identifier
                    + ", kolam email " + removedPoolLines
                    + " baris, SQLite " + removedSqliteRows
                    + " baris, sesi " + removedSessionFiles + " buah");
                return true;
#endif
            }
#pragma warning restore CS0162
            catch (Exception ex)
            {
                Log("Gagal menghapus:" + SensitiveDataSanitizer.Redact(row.Identifier) + " " + SensitiveDataSanitizer.Redact(ex.Message));
                return false;
            }
        }

#if LEGACY_DELETE_CODE
        private bool DeletionEmailMatch(string candidate, string emailKey)
        {
            if (emailKey.Length == 0) return false;
            string normalizedCandidate = NormalizeEmailKey(candidate);
            return normalizedCandidate.Length > 0 && normalizedCandidate == emailKey;
        }

        private int DeleteMailboxLines(PoolRow row, string emailKey)
        {
            int removed = 0;
            var paths = GetKnownMailboxPoolFiles().ToList();
            if (!string.IsNullOrWhiteSpace(row.SourcePath)
                && row.SourcePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                && File.Exists(row.SourcePath))
            {
                paths.Insert(0, row.SourcePath);
            }
            var exactLines = new[] { row.RawLine, row.MailboxLine };
            foreach (string path in paths.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                removed += MailboxPoolFileStore.DeleteMatchingLines(path, emailKey, exactLines);
            }
            return removed;
        }

        private int DeleteSqliteAccountRows(PoolRow row, string emailKey)
        {
            string dbPath = row.SourcePath.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase)
                ? row.SourcePath
                : GetDatabasePath();
            if (!File.Exists(dbPath)) return 0;

            var rows = SqliteNative.Query(dbPath, "SELECT id,email,json_path FROM accounts");
            var deleteIds = new List<string>();
            string explicitId = row.SourcePath.EndsWith(".sqlite3", StringComparison.OrdinalIgnoreCase) ? OnlyDigits(row.RawLine) : "";
            foreach (Dictionary<string, string> data in rows)
            {
                string id = data.TryGetValue("id", out string rawId) ? rawId : "";
                string email = data.TryGetValue("email", out string rawEmail) ? rawEmail : "";
                bool matches = explicitId.Length > 0 && id == explicitId;
                matches = matches || DeletionEmailMatch(email, emailKey);
                if (!matches) continue;
                deleteIds.Add(id);

                string jsonPath = data.TryGetValue("json_path", out string rawJsonPath) ? rawJsonPath : "";
                if (File.Exists(jsonPath) && IsUnderDirectory(jsonPath, GetSessionsDir()))
                {
                    TryDeleteFile(jsonPath);
                }
            }

            foreach (string id in deleteIds.Distinct())
            {
                SqliteNative.Execute(dbPath, "DELETE FROM accounts WHERE id=" + OnlyDigits(id));
            }
            return deleteIds.Distinct().Count();
        }

        private int DeleteSessionJsonFiles(PoolRow row, string emailKey)
        {
            int removed = 0;
            var dirs = new List<string> { GetSessionsDir(), rootDir };
            foreach (string dir in dirs.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (string path in Directory.GetFiles(dir, "session_*.json", SearchOption.TopDirectoryOnly))
                {
                    if (!SessionJsonMatchesEmail(path, emailKey)) continue;
                    if (TryDeleteFile(path)) removed++;
                }
            }
            string notes = (row.Notes ?? "").Trim();
            if (File.Exists(notes) && notes.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                && IsUnderDirectory(notes, GetSessionsDir()) && TryDeleteFile(notes))
            {
                removed++;
            }
            return removed;
        }

        private bool SessionJsonMatchesEmail(string path, string emailKey)
        {
            if (emailKey.Length == 0) return false;
            try
            {
                Dictionary<string, object> data = ReadJsonObject(path);
                return DeletionEmailMatch(GetString(data, "email"), emailKey);
            }
            catch
            {
                return false;
            }
        }

        private bool TryDeleteFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                Log("Gagal menghapus file:" + path + " " + ex.Message);
                return false;
            }
        }
#endif

        private bool TryDeleteFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return false;
                File.Delete(path);
                return true;
            }
            catch (Exception ex)
            {
                Log("Gagal menghapus file:" + SensitiveDataSanitizer.Redact(path) + " " + SensitiveDataSanitizer.Redact(ex.Message));
                return false;
            }
        }

        private void CancelBatch_Click(object sender, RoutedEventArgs e)
        {
            if (!backendTasks.IsRunning)
            {
                Log("Tidak ada batch yang sedang berjalan saat ini.");
                return;
            }
            try
            {
                if (backendTasks.Cancel())
                    Log("Batch saat ini telah dibatalkan.");
            }
            catch (Exception ex)
            {
                Log("Gagal membatalkan:" + ex.Message);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshPools();

        private void Settings_Click(object sender, RoutedEventArgs e) => ShowConfigDialog();
    }
}
