using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace SMOapp
{
    /// <summary>
    /// Главное окно приложения для анализа систем массового обслуживания (СМО).
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => cboSMOType.SelectedIndex = 0;
            QueuePanel.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Изменяет видимость поля размера очереди в зависимости от типа СМО.
        /// </summary>
        private void CboSMOType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QueuePanel == null || cboSMOType.SelectedItem == null) return;

            string type = ((ComboBoxItem)cboSMOType.SelectedItem).Content.ToString();
            QueuePanel.Visibility = (type == "СМО с ограниченной очередью")
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Основной метод расчёта выбранного типа СМО.
        /// </summary>
        private void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            txtResult.Clear();

            if (!ValidateInput(out int n, out double lambda, out double mu, out int m))
                return;

            // Если μ не введено, рассчитываем через tобс
            if (mu <= 0)
            {
                if (!double.TryParse(txtTob.Text, out double tob) || tob <= 0)
                {
                    MessageBox.Show("Введите корректное время обслуживания tобс > 0", "Ошибка");
                    return;
                }
                mu = 1.0 / tob;
            }

            string type = ((ComboBoxItem)cboSMOType.SelectedItem).Content.ToString();
            string result;

            switch (type)
            {
                case "СМО с отказами":
                    result = CalculateLossSystem(n, lambda, mu);
                    break;

                case "СМО с ограниченной очередью":
                    result = CalculateLimitedQueueSystem(n, m, lambda, mu);
                    break;

                case "СМО с неограниченной очередью":
                    result = CalculateUnlimitedQueueSystem(n, lambda, mu);
                    break;

                default:
                    result = "Неизвестный тип СМО";
                    break;
            }

            txtResult.Text = result;
        }

        // ====================== РАСЧЁТЫ ======================

        /// <summary>
        /// Расчёт СМО с отказами (Erlang B).
        /// </summary>
        private string CalculateLossSystem(int n, double lambda, double mu)
        {
            var sb = new StringBuilder();
            double rho = lambda / mu;
            double sum = 0;

            sb.AppendLine("=== СМО С ОТКАЗАМИ (Erlang B) ===");
            sb.AppendLine("----------------------------------------");

            for (int k = 0; k <= n; k++)
                sum += Math.Pow(rho, k) / Factorial(k);

            double P0 = 1.0 / sum;
            double Pblock = Math.Pow(rho, n) / Factorial(n) * P0;
            double L = 0;
            for (int k = 1; k <= n; k++)
                L += k * (Math.Pow(rho, k) / Factorial(k) * P0);

            double U = L / n;

            sb.AppendLine($"λ = {lambda:F4}");
            sb.AppendLine($"μ = {mu:F4}");
            sb.AppendLine($"ρ = {rho:F4}");
            sb.AppendLine($"Вероятность отказа (Pблок) = {Pblock:F5}");
            sb.AppendLine($"Среднее число занятых каналов = {L:F4}");
            sb.AppendLine($"Коэффициент загрузки каналов = {U:P2}");

            return sb.ToString();
        }

        /// <summary>
        /// Расчёт СМО с ограниченной очередью.
        /// </summary>
        private string CalculateLimitedQueueSystem(int n, int m, double lambda, double mu)
        {
            var sb = new StringBuilder();
            double rho = lambda / mu;
            int max = n + m;
            double sum = 0;

            sb.AppendLine("=== СМО С ОГРАНИЧЕННОЙ ОЧЕРЕДЬЮ ===");
            sb.AppendLine("----------------------------------------");

            for (int k = 0; k <= max; k++)
                sum += Math.Pow(rho, k) / Factorial(Math.Min(k, n));

            double P0 = 1.0 / sum;
            double Pblock = Math.Pow(rho, max) / Factorial(n) * P0;

            double Lq = 0;
            for (int k = n + 1; k <= max; k++)
                Lq += (k - n) * (Math.Pow(rho, k) / Factorial(n) * P0);

            double Wq = (lambda * (1 - Pblock) > 0) ? Lq / (lambda * (1 - Pblock)) : 0;

            sb.AppendLine($"n = {n}, m = {m}");
            sb.AppendLine($"λ = {lambda:F4}, μ = {mu:F4}, ρ = {rho:F4}");
            sb.AppendLine($"Вероятность отказа = {Pblock:F5}");
            sb.AppendLine($"Средняя длина очереди Lq = {Lq:F4}");
            sb.AppendLine($"Среднее время ожидания Wq = {Wq:F4} ед. времени");

            return sb.ToString();
        }

        /// <summary>
        /// Расчёт СМО с неограниченной очередью (M/M/n).
        /// </summary>
        private string CalculateUnlimitedQueueSystem(int n, double lambda, double mu)
        {
            var sb = new StringBuilder();
            double rho = lambda / (n * mu);

            sb.AppendLine("=== СМО С НЕОГРАНИЧЕННОЙ ОЧЕРЕДЬЮ (M/M/n) ===");
            sb.AppendLine("----------------------------------------");

            if (rho >= 1)
            {
                sb.AppendLine("Система НЕУСТОЙЧИВА (ρ ≥ 1). Очередь растёт бесконечно.");
                return sb.ToString();
            }

            double P0 = 1.0 / (Math.Pow(rho, n) / Factorial(n) / (1 - rho / n) +
                               Enumerable.Range(0, n).Sum(k => Math.Pow(rho, k) / Factorial(k)));

            double Lq = P0 * Math.Pow(rho, n) * rho / (Factorial(n) * Math.Pow(1 - rho / n, 2));
            double Wq = Lq / lambda;
            double W = Wq + 1 / mu;
            double L = lambda * W;

            sb.AppendLine($"n = {n}");
            sb.AppendLine($"λ = {lambda:F4}, μ = {mu:F4}, ρ = {rho:F4}");
            sb.AppendLine($"Средняя длина очереди Lq = {Lq:F4}");
            sb.AppendLine($"Среднее время ожидания Wq = {Wq:F4}");
            sb.AppendLine($"Среднее время пребывания в системе W = {W:F4}");
            sb.AppendLine($"Среднее число заявок в системе L = {L:F4}");

            return sb.ToString();
        }

        /// <summary>
        /// Проверка корректности введённых данных.
        /// </summary>
        private bool ValidateInput(out int n, out double lambda, out double mu, out int m)
        {
            n = 0; lambda = 0; mu = 0; m = 0;

            if (!int.TryParse(txtChannels.Text, out n) || n <= 0)
            {
                MessageBox.Show("Количество каналов n должно быть целым числом больше 0.", "Ошибка");
                return false;
            }

            if (!double.TryParse(txtLambda.Text, out lambda) || lambda <= 0)
            {
                MessageBox.Show("Интенсивность поступления λ должна быть больше 0.", "Ошибка");
                return false;
            }

            if (!double.TryParse(txtMu.Text, out mu) || mu < 0)
            {
                MessageBox.Show("Интенсивность обслуживания μ должна быть неотрицательной.", "Ошибка");
                return false;
            }

            string type = ((ComboBoxItem)cboSMOType.SelectedItem).Content.ToString();
            if (type == "СМО с ограниченной очередью")
            {
                if (!int.TryParse(txtQueue.Text, out m) || m < 0)
                {
                    MessageBox.Show("Размер очереди m должен быть неотрицательным целым числом.", "Ошибка");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Вычисляет факториал числа.
        /// </summary>
        private double Factorial(int k)
        {
            if (k <= 1) return 1;
            double res = 1;
            for (int i = 2; i <= k; i++)
                res *= i;
            return res;
        }

        /// <summary>
        /// Очищает все поля ввода и результат.
        /// </summary>
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            txtChannels.Clear();
            txtLambda.Clear();
            txtMu.Clear();
            txtTob.Clear();
            txtQueue.Clear();
            txtResult.Clear();
            cboSMOType.SelectedIndex = 0;
        }

        // ====================== РАБОТА С ФАЙЛАМИ ======================

        /// <summary>
        /// Загружает параметры СМО из текстового файла.
        /// </summary>
        private void BtnLoadFromFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Filter = "Текстовый файл (*.txt)|*.txt",
                Title = "Загрузить параметры СМО"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                string[] lines = File.ReadAllLines(dlg.FileName);

                if (lines.Length < 4)
                {
                    MessageBox.Show("Неверный формат файла.", "Ошибка");
                    return;
                }

                // Пример формата:
                // СМО с отказами
                // 5
                // 10
                // 2
                // 3

                string type = lines[0].Trim();
                int n = int.Parse(lines[1]);
                double lambda = double.Parse(lines[2]);
                double mu = double.Parse(lines[3]);

                txtChannels.Text = n.ToString();
                txtLambda.Text = lambda.ToString();
                txtMu.Text = mu.ToString();

                // Определяем тип
                if (type.Contains("отказ")) cboSMOType.SelectedIndex = 0;
                else if (type.Contains("ограничен")) cboSMOType.SelectedIndex = 1;
                else cboSMOType.SelectedIndex = 2;

                // Если есть m
                if (lines.Length > 4 && int.TryParse(lines[4], out int m))
                    txtQueue.Text = m.ToString();

                MessageBox.Show("Параметры успешно загружены!", "Загрузка");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки файла:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Сохраняет результаты расчёта в файл.
        /// </summary>
        private void BtnSaveResult_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtResult.Text))
            {
                MessageBox.Show("Сначала выполните расчёт.", "Предупреждение");
                return;
            }

            SaveFileDialog dlg = new SaveFileDialog
            {
                Filter = "Текстовый файл (*.txt)|*.txt",
                FileName = $"СМО_результат_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, txtResult.Text, Encoding.UTF8);
                    MessageBox.Show("Результат успешно сохранён!", "Сохранение");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка сохранения: {ex.Message}");
                }
            }
        }
    }
}