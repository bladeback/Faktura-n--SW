using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace InvoiceApp.Services
{
    public class BackupService
    {
        // List of files to backup
        private readonly string[] _filesToBackup = new[]
        {
            "documents.json",
            "invoice_counter.json",
            "suppliers.json",
            "customers.json",
            "saved_items.json",
            "config.json",
            "banks.json" // Include banks just in case, though it's static usually
        };

        public async Task CreateBackupAsync(string destinationPath)
        {
            await Task.Run(() =>
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;

                // Ensure destination doesn't exist or overwrite
                if (File.Exists(destinationPath)) File.Delete(destinationPath);

                using var archive = ZipFile.Open(destinationPath, ZipArchiveMode.Create);
                
                foreach (var fileName in _filesToBackup)
                {
                    var sourceFile = Path.Combine(baseDir, fileName);
                    if (File.Exists(sourceFile))
                    {
                        archive.CreateEntryFromFile(sourceFile, fileName);
                    }
                }
            });
        }

        public async Task RestoreBackupAsync(string sourcePath)
        {
            await Task.Run(() =>
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;

                using var archive = ZipFile.OpenRead(sourcePath);
                
                foreach (var entry in archive.Entries)
                {
                    // Only extract known files for security
                    bool isKnown = false;
                    foreach (var known in _filesToBackup)
                    {
                        if (entry.Name.Equals(known, StringComparison.OrdinalIgnoreCase))
                        {
                            isKnown = true;
                            break;
                        }
                    }

                    if (isKnown)
                    {
                        var destFile = Path.Combine(baseDir, entry.Name);
                        entry.ExtractToFile(destFile, true); // Overwrite
                    }
                }
            });
        }
    }
}
