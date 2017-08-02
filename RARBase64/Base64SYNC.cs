using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Base64
{
    /// <summary>
    /// Класс, реализующий кодирование/декодирование данных по протоколу Base64 (с синхронизацией).
    /// </summary>
    public static class Base64SYNC
    {
        /// <summary>Размер блока Base64.</summary>
        private const int _baseBlock64Size = 4;

        /// <summary>Размер блока Base64.</summary>
        private const int _syncBlockSize = _baseBlock64Size * 16; // 64 символа.

        /// <summary>Символ выравнивания потока Base64.</summary>
        private const char _padSymbol = '=';

        /// <summary>Символ синхронизации Base64SYNC.</summary>
        private const char _syncSymbol = '\n';

        /// <summary>Открывающий заголовок Base64 потока.</summary>
        private const string _header = "------------------- BEGIN RARBase64 MESSAGE --------------------\n";

        /// <summary>Закрывающий заголовок Base64 потока.</summary>
        private const string _footer = "-------------------- END RARBase64 MESSAGE ---------------------";

        /// <summary>Маркер страницы.</summary>
        private const string _pageMarker = ":::::RARBase64 PAGE MARKER:::::";

        /// <summary>Количество строк до следующего маркера страниц.</summary>
        private const int _rowsToPageMarkerCounter = 32;

        /// <summary>Выравнивание номера страниц точками справа.</summary>
        private const int _dotPageNumPadding = 4;

        /// <summary>
        /// Получение хеш-множества символов, принадлежащих Base64.
        /// </summary>
        /// <param name="isExtended">Расширенное множество?</param>
        /// <returns>Хеш-множество символов, принадлежащих Base64.</returns>
        public static HashSet<byte> GetBase64HashSet(bool isExtended = false)
        {
            var base64HashSet = new HashSet<byte>();

            foreach(char c in "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/")
            {
                base64HashSet.Add((byte)c);
            }

            if(isExtended)
            {
                base64HashSet.Add((byte)_padSymbol);
            }

            return base64HashSet;
        }

        /// <summary>
        /// Очистка входной строки от символов, не принадлежащих Base64.
        /// </summary>
        /// <param name="source">Исходная строка.</param>
        /// <param name="isExtended">Расширенный набор символов?</param>
        /// <returns>Строка, содержащая набор Base64.</returns>
        public static string GetBase64String(string source, bool isExtended = false)
        {
            // Хеш-множество кодировки Base64
            HashSet<byte> base64 = GetBase64HashSet(isExtended);

            // Здесь будем хранить формируемую строку
            var clean = new StringBuilder();

            // Обрабатываем каждый символ входной последовательности...
            foreach(char c in source)
            {
                if(base64.Contains((byte)c))
                {
                    clean.Append((char)((byte)c));
                }
            }

            // Шлем отфильтрованную по стандарту Base64 строку...
            return clean.ToString();
        }

        /// <summary>
        /// Вычисление выравнивания по границе.
        /// </summary>
        /// <param name="dataLen">Текущая длина выравниваемых данных.</param>
        /// <param name="blockSize">Размер блока, по которому будет осуществляться выравнивание.</param>
        /// <returns>Вычисленная величина выравнивания.</returns>
        private static int GetPadding(int dataLen, int blockSize)
        {
            return (blockSize - dataLen % blockSize) % blockSize;
        }

        /// <summary>
        /// Кодирование двоичного файла в Base64SYNC.
        /// </summary>
        /// <param name="binFilename">Исходный двоичный файл.</param>
        /// <param name="base64Filename">Целевой Base64-файл.</param>
        public static void EncodeFile(string binFilename, string base64Filename)
        {
            // Получаем "сырой" формат Base64, без маркеров.
            string rawBase64 = Convert.ToBase64String(File.ReadAllBytes(binFilename));

            // Проверка на вхождение служебных маркеров в сгенерированный поток...
            if((rawBase64.IndexOf(_header) != -1) || (rawBase64.IndexOf(_footer) != -1) || (rawBase64.IndexOf(_pageMarker) != -1))
            {
                //...если таковые встретились - выходим без обработки...
                return;
            }

            // Выравниваем первоначальный поток по безопасной границе...
            rawBase64 = rawBase64.Replace(_padSymbol, '+');
            var safePadding = _syncBlockSize - (rawBase64.Length % _syncBlockSize);
            if(safePadding != _syncBlockSize)
            {
                rawBase64 += new string('+', safePadding);
            }

            // Здесь будем хранить формируемую строку в формате "Base64"...
            var base64_ = new StringBuilder();

            // Пишем заглавный маркер потока...
            base64_.Append(_header);

            // Отмечаем блоки Base64 для надежной синхронизации синхромаркером...
            int symbolsRecorded = 0;
            int rowsToPageMarkerCounter = _rowsToPageMarkerCounter;
            int pageNumCounter = 1; // Номер текущей страницы.
            var pageNum_ = new StringBuilder();

            // Цикл разбиения текста на синхроблоки...
            for(int i = 0; i <= rawBase64.Length - _syncBlockSize; i += _syncBlockSize)
            {
                base64_.Append(rawBase64.Substring(i, _syncBlockSize) + _syncSymbol);
                symbolsRecorded += _syncBlockSize;

                // Если достигли точки маркировки страницы...
                if(--rowsToPageMarkerCounter == 0)
                {
                    // В первую очередь сбрасываем счетчик строк, оставшихся до маркера страницы...
                    rowsToPageMarkerCounter = _rowsToPageMarkerCounter;

                    // Формируем строку, идентифицирующую текущую страницу, но итоговой длиной не более максимума...
                    pageNum_.Clear();
                    pageNum_.Append(_pageMarker);
                    int iterNum = 0;
                    while((pageNum_ + pageNumCounter.ToString()).Length + _dotPageNumPadding < _syncBlockSize)
                    {
                        if(++iterNum > 1) pageNum_.Append("/");
                        pageNum_.Append(pageNumCounter.ToString());
                    }
                    while((pageNum_ + ":").Length <= _syncBlockSize)
                    {
                        pageNum_.Append(":");
                    }
                    base64_.Append(pageNum_);
                    base64_.Append(_syncSymbol);
                    pageNumCounter++;
                }
            }

            // Вычисляем остаток, подлежащий записи...
            int remainSymbolsCount = rawBase64.Length - symbolsRecorded;
            string remainSymbols = rawBase64.Substring(symbolsRecorded, remainSymbolsCount);
            if(remainSymbols.Length != 0)
            {
                base64_.Append(remainSymbols + _syncSymbol);
            }
            base64_.Append(_footer);

            //...и, завершив поток маркером, сбрасываем данные в файл.
            File.WriteAllText(base64Filename, base64_.ToString());
        }

        /// <summary>
        /// Декодирование двоичного файла из Base64SYNC.
        /// </summary>
        /// <param name="base64Filename">Исходный Base64-файл.</param>
        /// <param name="binFilename">Целевой двоичный файл.</param>
        public static void DecodeFile(string base64Filename, string binFilename)
        {
            // Считываем данные с "синхроимпульсами"...
            string base64WithSync = File.ReadAllText(base64Filename);

            // Убираем из исходного потока заголовки...
            base64WithSync = base64WithSync.Replace(_header, string.Empty).Replace(_footer, string.Empty);

            // Разбиваем исходный поток на строки (убирая пустые)...
            string[] base64ByLines = base64WithSync.Split(new[] {_syncSymbol}, StringSplitOptions.RemoveEmptyEntries);

            // Накопитель выходного потока...
            var base64_ = new StringBuilder();

            // Обрабатываем каждую строку...            
            foreach(string s in base64ByLines)
            {
                // Если строка начинается не с маркера страницы - добавляем её в выходной поток...
                if(!s.StartsWith(_pageMarker))
                {
                    //...очищая от чужеродных символов...
                    string cs = GetBase64String(s);

                    //...и выделяя с её конца кратный 4 синхроблок...
                    if(cs.Length >= _baseBlock64Size)
                    {
                        int blockOffset = cs.Length % _baseBlock64Size;
                        int blockToCopySize = cs.Length - blockOffset;
                        cs = cs.Substring(blockOffset, blockToCopySize);
                        if(cs.Length % _baseBlock64Size == 0) base64_.Append(cs);
                    }
                }
            }

            // Поддерживаем padding потока Base64...
            int paddingCount = GetPadding(base64_.Length, _baseBlock64Size);
            for(int i = 0; i < paddingCount; i++)
            {
                base64_.Append(_padSymbol);
            }

            // Если выходной файл уже существует...
            if(File.Exists(binFilename))
            {
                //...удаляем его...
                File.SetAttributes(binFilename, FileAttributes.Normal);
                File.Delete(binFilename);
            }

            //...затем записываем все данные в целевой поток
            File.WriteAllBytes(binFilename, Convert.FromBase64String(base64_.ToString()));
        }
    }
}
