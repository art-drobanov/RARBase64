using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

using Base64;

namespace RARBase64
{
    class Program
    {
        /// <summary>
        /// Версия приложения.
        /// </summary>
        private static string _version = "v.1.0";

        /// <summary>
        /// Расширение генерируемого выходного файла.
        /// </summary>
        private static string _ext = ".RARBase64"; // 10 символов

        /// <summary>
        /// Имя временного файла.
        /// </summary>
        private static readonly string _tmp = Guid.NewGuid().ToString("B") + ".rar";

        /// <summary>
        /// Префикс для вылеченного файла.
        /// </summary>
        private static string _fixed = "fixed.";

        /// <summary>
        /// Запуск консольного архиватора.
        /// </summary>
        /// <param name="args">Аргументы командной строки.</param>
        private static void RarRun(string args)
        {
            // Cоздание параметров...
            var startInfo = new ProcessStartInfo
                                {
                                    // Имя исполняемого файла...
                                    FileName = "rar.exe",
                
                                    // Окно делаем скрытым...
                                    WindowStyle = ProcessWindowStyle.Hidden,

                                    //...и добавляем к нему аргументы командной строки
                                    Arguments = args
                                };

            // Запускаем процесс...
            Process rarProc = Process.Start(startInfo);

            //...и ждём его завершения.
            rarProc.WaitForExit();
        }

        /// <summary>
        /// Компрессия и помехоустойчивое кодирование данных.
        /// </summary>
        /// <param name="args">Аргументы командной строки.</param>
        private static void Encode(string[] args)
        {
            string binaryName = args[0];
            string encodedName = binaryName + _ext;

            // Сжимаем данные...
            RarRun("a -m5 -ma5 " + _tmp + " " + binaryName);

            //...и добавляем данные для восстановления (100%)
            RarRun("rr100% " + _tmp);

            // После сжатия данных архиватором, двоичный файл - это временный файл...
            binaryName = _tmp;

            try
            {
                // Если нет того, что нужно обрабатывать, выходим...
                if((binaryName == string.Empty) || (!File.Exists(binaryName)))
                {
                    return;
                }

                // Обеспечиваем Base64 кодирование...
                Base64SYNC.EncodeFile(binaryName, encodedName);

                // Удаляем временный файл...
                if(File.Exists(_tmp)) File.Delete(_tmp);
            }
            catch
            {
            }
        }

        /// <summary>
        /// Помехоустойчивое декодирование и декомпрессия данных.
        /// </summary>
        /// <param name="args">Аргументы командной строки.</param>
        private static void Decode(string[] args)
        {
            string encodedName = args[0];

            try
            {
                // Если нет того, что нужно обрабатывать, выходим...
                if((encodedName == string.Empty) || (!File.Exists(encodedName)))
                {
                    return;
                }

                // Обеспечиваем Base64 декодирование...
                Base64SYNC.DecodeFile(encodedName, _tmp);
            }
            catch
            {
                return;
            }

            // Определяем, имеет ли архив повреждения...
            RarRun("r " + _tmp);

            // Формируем имя восстановленного файла...
            string fixedName = _fixed + _tmp;
            string binaryName;

            // Если существует файл с префиксом "fixed." - имели место повреждения данных.
            if(File.Exists(fixedName))
            {
                binaryName = fixedName;
            }
            else
            {
                binaryName = _tmp;
            }

            // Запускаем извлечение данных из архива...
            RarRun("e -kb -o+" + " " + binaryName);

            // Удаляем временные файлы...
            if(File.Exists(_tmp)) File.Delete(_tmp);
            if(File.Exists(binaryName)) File.Delete(binaryName);
        }

        private static void Main(string[] args)
        {
            // Если не найден исполняемый файл RAR...
            if(!File.Exists("rar.exe"))
            {
                MessageBox.Show("Rar.exe (from WinRAR5) is not found!",
                                "RARBase64" + " " + _version + " " + "Help", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);

                return;
            }

            // Если нет аргументов...
            if(args.Length == 0)
            {
                MessageBox.Show("Drag and drop the file to be auto-processed on RARBase64, or use syntax: \n\"RARBase64.exe <input>\"",
                                "RARBase64" + " " + _version + " " + "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return;
            }

            // Обеспечиваем нормальное функционирование временного файла...
            if(File.Exists(_tmp))
            {
                File.SetAttributes(_tmp, FileAttributes.Normal);
                File.Delete(_tmp);
            }

            // Если имя исходного файла меньше специфического расширения, то это 100% не Base64...
            if(args[0].Length < _ext.Length)
            {
                Encode(args);
                return;
            }

            // Если имя исходного файла содержит специфическое расширение, то требуется декодирование...
            string s = args[0].Substring(args[0].Length - _ext.Length, _ext.Length).ToLower();
            if(s == _ext.ToLower())
            {
                Decode(args);
                return;
            }

            // Если дошли до данного участка кода - остается только кодировать :)
            Encode(args);
        }
    }
}
