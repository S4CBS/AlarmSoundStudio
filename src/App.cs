// AlarmSoundStudio - свои звуки будильника для приложения Windows "Часы".
// Никаких жёстко зашитых значений: слоты обнаруживаются динамически из реестра,
// дефолтные звуки читаются из HKLM, сброс = удаление пользовательской записи.
// Конвертация - Media Foundation через Windows PowerShell (есть на любом Win10/11).
// C# 5: компиляция системным csc.exe, WPF, тёмная тема. Права администратора не нужны.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace AlarmSoundStudio
{
    public class SlotInfo
    {
        public int Index;        // порядковый номер для отображения (1..N)
        public string Event;     // имя системного события, напр. "Notification.Looping.Alarm5"
        public string Default;   // системный звук по умолчанию (HKLM) или null
    }

    public class CardRefs
    {
        public SlotInfo Slot;
        public Border Card;
        public TextBlock Current;
        public TextBlock State;
    }

    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            var app = new Application();
            var win = new MainWindow();
            app.Run(win);
        }
    }

    public class MainWindow : Window
    {
        // ---------- пути и ключи реестра ----------
        static readonly string EventsRoot = @"AppEvents\Schemes\Apps\.Default\";
        static readonly string BasePrefix = "Notification.Looping.Alarm";
        static readonly string StoreDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlarmSoundStudio");

        SlotInfo[] slots;
        List<CardRefs> cards;
        TextBlock status;
        string chosenFile;
        System.Media.SoundPlayer player = new System.Media.SoundPlayer();
        Button btnAdd, btnApply, btnPlay, btnStop, btnReset, btnMod;
        ComboBox cmbTarget;

        // палитра
        Brush BgWin      = Brush("#1E1E26");
        Brush BgCard     = Brush("#262630");
        Brush BgCardSel  = Brush("#30303E");
        Brush BgControl  = Brush("#33333F");
        Brush Accent     = Brush("#4CC2FF");
        Brush AccentDim  = Brush("#2A6E8F");
        Brush FgMain     = Brush("#ECECEC");
        Brush FgDim      = Brush("#9A9AA8");
        Brush FgGreen    = Brush("#5EE39A");
        Brush FgRed      = Brush("#FF7B7B");

        static Brush Brush(string hex)
        {
            var b = new BrushConverter();
            return (Brush)b.ConvertFromString(hex);
        }

        public MainWindow()
        {
            Title = "AlarmSoundStudio — свои звуки будильника для «Часов»";
            Width = 780; Height = 640;
            MinWidth = 700; MinHeight = 560;
            Background = BgWin;
            FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = new StackPanel { Margin = new Thickness(20, 16, 20, 4) };
            header.Children.Add(new TextBlock {
                Text = "Мои звуки будильника",
                FontSize = 24, FontWeight = FontWeights.Bold, Foreground = FgMain
            });
            header.Children.Add(new TextBlock {
                Text = "Слоты обнаружены автоматически в вашей системе (события Windows Notification.Looping.Alarm*).\n" +
                       "Выбранный файл конвертируется в WAV и назначается на событие — приложение «Часы» начинает играть его.",
                FontSize = 13, Foreground = FgDim, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap
            });
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var scroll = new ScrollViewer {
                Margin = new Thickness(20, 8, 20, 8),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            var listPanel = new StackPanel();
            cards = new List<CardRefs>();

            slots = DiscoverSlots();
            if (slots.Length == 0) {
                listPanel.Children.Add(new TextBlock {
                    Text = "Не найдено ни одного события Notification.Looping.Alarm* — эта система не поддерживает будильники «Часов».",
                    Foreground = FgRed, FontSize = 14, Margin = new Thickness(8)
                });
            }
            foreach (var s in slots) {
                var card = BuildCard(s);
                listPanel.Children.Add(card.Card);
                cards.Add(card);
            }
            scroll.Content = listPanel;
            Grid.SetRow(scroll, 2);
            root.Children.Add(scroll);

            var controls = new WrapPanel { Margin = new Thickness(20, 4, 20, 4) };
            btnAdd = MakeButton("Добавить звук…", Accent, Brushes.Black, BtnAdd_Click, 150);
            cmbTarget = new ComboBox {
                Width = 240, Height = 32, Margin = new Thickness(8, 0, 8, 0),
                Background = BgControl, Foreground = FgMain, VerticalContentAlignment = VerticalAlignment.Center
            };
            cmbTarget.Items.Add("На все слоты");
            foreach (var s in slots) cmbTarget.Items.Add("Слот " + s.Index);
            if (slots.Length > 0) cmbTarget.SelectedIndex = 0;
            btnApply = MakeButton("Применить", AccentDim, FgMain, BtnApply_Click, 110);
            btnPlay = MakeButton("▶", BgControl, FgMain, BtnPlay_Click, 40);
            btnStop = MakeButton("■", BgControl, FgMain, BtnStop_Click, 40);
            btnMod = MakeButton("Имя звука в «Часах»…", BgControl, FgMain, BtnMod_Click, 170);
            btnReset = MakeButton("Сбросить всё", BgControl, FgDim, BtnReset_Click, 110);
            foreach (var c in new UIElement[] { btnAdd, cmbTarget, btnApply, btnPlay, btnStop, btnMod, btnReset })
                controls.Children.Add(c);
            Grid.SetRow(controls, 3);
            root.Children.Add(controls);

            status = new TextBlock {
                Text = "Готово. Выберите аудиофайл (mp3, wav, m4a, ogg и т.д.) и нажмите «Применить».",
                Foreground = FgDim, FontSize = 12,
                Margin = new Thickness(20, 4, 20, 12), TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(status, 4);
            root.Children.Add(status);

            Content = root;
            RefreshCards();
        }

        // ---------- динамическое обнаружение слотов ----------

        SlotInfo[] DiscoverSlots()
        {
            var events = new List<string>();

            // 1) канонические имена событий Windows (прямой пробинг ключей - работает даже там,
            //    где перечисление подразделов запрещено политикой)
            try {
                string baseName = BasePrefix;
                if (Registry.LocalMachine.OpenSubKey(EventsRoot + baseName) != null ||
                    Registry.CurrentUser.OpenSubKey(EventsRoot + baseName) != null) events.Add(baseName);
                for (int n = 2; n <= 10; n++) {
                    string name = BasePrefix + n;
                    if (Registry.LocalMachine.OpenSubKey(EventsRoot + name) != null ||
                        Registry.CurrentUser.OpenSubKey(EventsRoot + name) != null) events.Add(name);
                }
            } catch { }

            // 2) плюс всё, что найдено перечислением (будущие/нестандартные слоты)
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser }) {
                try {
                    using (var scheme = hive.OpenSubKey(EventsRoot)) {
                        if (scheme == null) continue;
                        foreach (var name in scheme.GetSubKeyNames()) {
                            if (IsAlarmEvent(name) && !events.Contains(name, StringComparer.OrdinalIgnoreCase))
                                events.Add(name);
                        }
                    }
                } catch { }
            }

            events.Sort(CompareAlarmEvents);

            var list = new List<SlotInfo>();
            int idx = 1;
            foreach (var ev in events) {
                var s = new SlotInfo();
                s.Index = idx++;
                s.Event = ev;
                s.Default = GetMachineDefault(ev);
                list.Add(s);
            }
            return list.ToArray();
        }

        static bool IsAlarmEvent(string name)
        {
            if (string.Equals(name, BasePrefix, StringComparison.OrdinalIgnoreCase)) return true;
            if (!name.StartsWith(BasePrefix, StringComparison.OrdinalIgnoreCase)) return false;
            var tail = name.Substring(BasePrefix.Length);
            int n;
            return int.TryParse(tail, out n) && n >= 1;   // Alarm2..Alarm10 (и любые будущие)
        }

        static int CompareAlarmEvents(string a, string b)
        {
            return NumOf(a).CompareTo(NumOf(b));   // базовое событие = 1, дальше по номеру
        }

        static int NumOf(string eventName)
        {
            var tail = eventName.Substring(BasePrefix.Length);
            int n;
            if (!int.TryParse(tail, out n)) return 1;   // базовое "Notification.Looping.Alarm" = слот 1
            return n;
        }

        string GetMachineDefault(string eventName)
        {
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser }) {
                using (var k = hive.OpenSubKey(EventsRoot + eventName + @"\.Default")) {
                    var v = k == null ? null : k.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            return null;
        }

        // ---------- эффективный звук слота ----------

        string GetEffectiveSound(string eventName)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(EventsRoot + eventName + @"\.Current")) {
                var v = k == null ? null : k.GetValue(null) as string;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return GetMachineDefault(eventName);
        }

        bool IsUserOverridden(string eventName)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(EventsRoot + eventName + @"\.Current")) {
                var v = k == null ? null : k.GetValue(null) as string;
                return !string.IsNullOrEmpty(v) &&
                       !string.Equals(v, GetMachineDefault(eventName), StringComparison.OrdinalIgnoreCase);
            }
        }

        void SetSlotSound(SlotInfo s, string wav)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(EventsRoot + s.Event + @"\.Current"))
                k.SetValue(null, wav);
        }

        /// <summary>Полный сброс: удаляем пользовательское значение - система возвращается к своему дефолту.</summary>
        void ResetSlot(SlotInfo s)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(EventsRoot + s.Event + @"\.Current", true)) {
                if (k != null) {
                    try { k.DeleteValue(null, false); } catch { }
                    try { k.Close(); } catch { }
                }
            }
            // ключ .Current мог не существовать изначально - удаляем его целиком, если он теперь пуст
            try {
                var parent = Registry.CurrentUser.OpenSubKey(EventsRoot + s.Event, true);
                if (parent != null && parent.GetSubKeyNames().Length == 0) {
                    parent.DeleteSubKey(".Current", false);
                }
                if (parent != null) parent.Close();
            } catch { }
        }

        // ---------- карточки ----------

        CardRefs BuildCard(SlotInfo s)
        {
            var card = new Border {
                Background = BgCard, CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 5, 0, 5), Padding = new Thickness(14, 10, 14, 10),
                Cursor = Cursors.Hand, Tag = s.Index
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var numBorder = new Border {
                Width = 32, Height = 32, CornerRadius = new CornerRadius(16),
                Background = BgControl, VerticalAlignment = VerticalAlignment.Center
            };
            var numText = new TextBlock {
                Text = s.Index.ToString(), Foreground = FgMain, FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            };
            numBorder.Child = numText;
            Grid.SetColumn(numBorder, 0);
            grid.Children.Add(numBorder);

            var nameCol = new StackPanel { Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            nameCol.Children.Add(new TextBlock { Text = "Слот " + s.Index, Foreground = FgMain, FontSize = 15, FontWeight = FontWeights.SemiBold });
            var current = new TextBlock { Foreground = FgDim, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis };
            nameCol.Children.Add(current);
            Grid.SetColumn(nameCol, 1);
            grid.Children.Add(nameCol);

            var state = new TextBlock { Foreground = FgDim, FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(state, 2);
            grid.Children.Add(state);

            card.Child = grid;
            card.MouseLeftButtonUp += (o, e) => SelectCard(s.Index);
            return new CardRefs { Slot = s, Card = card, Current = current, State = state };
        }

        void SelectCard(int index)
        {
            foreach (var c in cards) {
                bool sel = (c.Slot.Index == index);
                c.Card.Background = sel ? BgCardSel : BgCard;
                c.Card.BorderBrush = sel ? Accent : Brushes.Transparent;
                c.Card.BorderThickness = new Thickness(1);
            }
            int pos = 0;
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].Index == index) { pos = i + 1; break; }
            if (pos > 0 && cmbTarget.Items.Count > pos) cmbTarget.SelectedIndex = pos;
        }

        void RefreshCards()
        {
            foreach (var c in cards) {
                string cur = GetEffectiveSound(c.Slot.Event);
                c.Current.Text = string.IsNullOrEmpty(cur) ? "(не задано)" : cur;
                if (IsUserOverridden(c.Slot.Event)) {
                    c.State.Text = "★ СВОЙ ЗВУК";
                    c.State.Foreground = FgGreen;
                } else {
                    c.State.Text = "стандартный";
                    c.State.Foreground = FgDim;
                }
            }
        }

        // ---------- кнопки ----------

        Button MakeButton(string text, Brush bg, Brush fg, RoutedEventHandler onClick, double width)
        {
            var b = new Button {
                Content = text, Width = width, Height = 32, Margin = new Thickness(0, 0, 8, 8),
                Foreground = fg, Background = bg, FontSize = 13,
                Cursor = Cursors.Hand, BorderThickness = new Thickness(0)
            };
            var hover = Lighten(bg, 1.25);
            var normal = bg;
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "bd";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            border.SetValue(Border.BackgroundProperty, bg);
            border.SetValue(Border.PaddingProperty, new Thickness(10, 4, 10, 4));
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            border.AppendChild(presenter);
            template.VisualTree = border;
            b.Template = template;
            b.MouseEnter += (o, e) => SetBorderBg(b, hover);
            b.MouseLeave += (o, e) => SetBorderBg(b, normal);
            b.Click += onClick;
            return b;
        }

        void SetBorderBg(Button b, Brush brush)
        {
            var bd = FindBorder(b);
            if (bd != null) bd.Background = brush;
        }

        Border FindBorder(DependencyObject o)
        {
            for (int i = 0; i < 4; i++) {
                if (o is Border) return (Border)o;
                var child = VisualTreeHelper.GetChild(o, 0);
                if (child == null) return null;
                o = child;
            }
            return null;
        }

        Brush Lighten(Brush src, double k)
        {
            var c = ((SolidColorBrush)src).Color;
            int r = Math.Min(255, (int)(c.R * k));
            int g = Math.Min(255, (int)(c.G * k));
            int b2 = Math.Min(255, (int)(c.B * k));
            return new SolidColorBrush(Color.FromRgb((byte)r, (byte)g, (byte)b2));
        }

        // ---------- обработчики ----------

        void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog {
                Title = "Выберите аудиофайл для будильника",
                Filter = "Все аудиофайлы|*.mp3;*.wav;*.m4a;*.m4b;*.aac;*.wma;*.mp4;*.3gp;*.amr;*.adt;*.ogg;*.flac;*.opus|MP3|*.mp3|WAV|*.wav|Все файлы|*.*"
            };
            if (dlg.ShowDialog() == true) {
                chosenFile = dlg.FileName;
                SetStatus("Выбрано: " + chosenFile + "  —  нажмите «Применить».", FgMain);
            }
        }

        async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (slots.Length == 0) { SetStatus("Системные события будильника не найдены.", FgRed); return; }
            if (string.IsNullOrEmpty(chosenFile)) { SetStatus("Сначала выберите файл («Добавить звук…»).", FgDim); return; }
            int target = cmbTarget.SelectedIndex; // 0 = все, иначе slot = target
            btnApply.IsEnabled = false;
            SetStatus("Конвертация…", FgMain);
            try {
                string wav = await Task.Run(() => ResolveAudio(chosenFile));
                int[] targets = (target <= 0) ? slots.Select(s => s.Index).ToArray() : new[] { target };
                foreach (int i in targets) {
                    var s = slots.FirstOrDefault(x => x.Index == i);
                    if (s != null) SetSlotSound(s, wav);
                }
                RefreshCards();
                bool sc = EnsureShortcut();
                SetStatus(string.Format("Готово: назначено слотов — {0}. Файл: {1}{2}",
                    targets.Length, wav, sc ? "  |  Ярлык создан в меню «Пуск»." : ""), FgGreen);
            } catch (Exception ex) {
                string m = ex.Message;
                if (ex.InnerException != null) m = ex.InnerException.Message;
                SetStatus("Ошибка: " + m, FgRed);
            }
            btnApply.IsEnabled = true;
        }

        void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            try {
                int idx = cmbTarget.SelectedIndex;
                SlotInfo s = (idx <= 0 && slots.Length > 0) ? slots[0] : (idx >= 1 && idx <= slots.Length ? slots[idx - 1] : null);
                if (s == null) { SetStatus("Слот не выбран.", FgDim); return; }
                string p = GetEffectiveSound(s.Event);
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) { SetStatus("Файл слота не найден: " + p, FgDim); return; }
                player.Stop();
                player.SoundLocation = p;
                player.Play();
                SetStatus("Играет: " + p, FgMain);
            } catch (Exception ex) { SetStatus("Ошибка воспроизведения: " + ex.Message, FgRed); }
        }

        void BtnStop_Click(object sender, RoutedEventArgs e) { player.Stop(); SetStatus("Остановлено.", FgDim); }

        void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var s in slots) ResetSlot(s);
            RefreshCards();
            SetStatus("Пользовательские переназначения удалены — система вернулась к своим стандартным звукам.", FgDim);
        }

        void BtnMod_Click(object sender, RoutedEventArgs e)
        {
            var w = new ModWindow();
            w.Owner = this;
            w.ShowDialog();
        }

        void SetStatus(string text, Brush color)
        {
            status.Text = text;
            status.Foreground = color;
        }

        // ---------- конвертация и файлы ----------

        string ResolveAudio(string src)
        {
            if (!File.Exists(src)) throw new Exception("Файл не найден: " + src);
            if (!Directory.Exists(StoreDir)) Directory.CreateDirectory(StoreDir);
            string dst = Path.Combine(StoreDir, Path.GetFileNameWithoutExtension(src) + ".wav");
            string ext = Path.GetExtension(src).ToLowerInvariant();
            if (ext == ".wav" && IsWav(src)) {
                File.Copy(src, dst, true);
            } else {
                string engine = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "audio-engine.ps1");
                if (!File.Exists(engine)) throw new Exception("Не найден tools\\audio-engine.ps1 (должен лежать рядом с exe)");
                var psi = new ProcessStartInfo {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + engine + "\" -Src \"" + src + "\" -Dst \"" + dst + "\"",
                    UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
                    CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8
                };
                using (var p = Process.Start(psi)) {
                    string output = p.StandardOutput.ReadToEnd();
                    string errOut = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    output = (output ?? "").Trim();
                    if (!output.StartsWith("OK", StringComparison.Ordinal))
                        throw new Exception(output.StartsWith("ERR:", StringComparison.Ordinal)
                            ? output.Substring(4)
                            : "Конвертация не удалась. " + (errOut ?? "").Trim());
                }
            }
            if (!IsWav(dst)) throw new Exception("Результат не является корректным WAV (PCM). Формат может не поддерживаться кодеками системы — попробуйте mp3 или wav.");
            return dst;
        }

        static bool IsWav(string path)
        {
            try {
                var b = File.ReadAllBytes(path);
                if (b.Length < 44) return false;
                if (b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F') return false;
                if (b[8] != 'W' || b[9] != 'A' || b[10] != 'V' || b[11] != 'E') return false;
                int pos = 12;
                while (pos + 8 <= b.Length) {
                    string id = "" + (char)b[pos] + (char)b[pos + 1] + (char)b[pos + 2] + (char)b[pos + 3];
                    int sz = BitConverter.ToInt32(b, pos + 4);
                    if (id == "fmt ") {
                        int fmt = BitConverter.ToUInt16(b, pos + 8);
                        return fmt == 1 || fmt == 0xFFFE;   // PCM или EXTENSIBLE PCM
                    }
                    if (id == "data") return false;
                    pos += 8 + sz + (sz % 2);
                }
                return false;
            } catch { return false; }
        }

        bool shortcutChecked = false;
        bool EnsureShortcut()
        {
            if (shortcutChecked) return false;
            shortcutChecked = true;
            try {
                string lnkDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs");
                string lnk = Path.Combine(lnkDir, "AlarmSoundStudio.lnk");
                if (File.Exists(lnk)) return false;
                var shellType = Type.GetTypeFromProgID("WScript.Shell");
                dynamic shell = Activator.CreateInstance(shellType);
                dynamic sc = shell.CreateShortcut(lnk);
                sc.TargetPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AlarmSoundStudio.exe");
                sc.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                sc.IconLocation = Environment.SystemDirectory + "\\shell32.dll,12";
                sc.Description = "Свои звуки будильника для приложения Часы";
                sc.Save();
                return true;
            } catch { return false; }
        }
    }

    // ---------- окно "своё имя звука в списке Часов" (мод пакета) ----------

    public class ModWindow : Window
    {
        // EN-имена слотов в списке Часов (порядок = слоты 1..10)
        static readonly string[] EnNames = { "Chimes", "Xylophone", "Chords", "Tap", "Jingle", "Transition", "Descending", "Bounce", "Echo", "Ascending" };
        static readonly int[] MaxBytes = { 6, 9, 6, 3, 6, 10, 10, 6, 4, 9 };

        ComboBox cmb;
        TextBox txt;
        TextBlock hint, status;

        Brush BgWin = Brush("#1E1E26");
        Brush FgMain = Brush("#ECECEC");
        Brush FgDim = Brush("#9A9AA8");
        Brush FgGreen = Brush("#5EE39A");
        Brush FgRed = Brush("#FF7B7B");
        Brush Accent = Brush("#4CC2FF");
        Brush BgControl = Brush("#33333F");

        static Brush Brush(string hex)
        {
            var b = new BrushConverter();
            return (Brush)b.ConvertFromString(hex);
        }

        public ModWindow()
        {
            Title = "Своё имя звука в списке «Часов»";
            Width = 560; Height = 380;
            Background = BgWin;
            FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock {
                Text = "Переопределяет приложение «Часы» модифицированной копией, в которой\nзвук выбранного слота называется так, как вы введёте. Требуется UAC (1 раз).\nБудильники и настройки сохраняются. Откат: «Вернуть оригинал».",
                Foreground = FgDim, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
            });

            root.Children.Add(new TextBlock { Text = "Слот (позиция в списке «Часов»):", Foreground = FgMain, FontSize = 13 });
            cmb = new ComboBox { Height = 30, Margin = new Thickness(0, 4, 0, 10) };
            for (int i = 1; i <= 10; i++) cmb.Items.Add(string.Format("Слот {0} ({1})", i, EnNames[i - 1]));
            cmb.SelectedIndex = 2;   // "Chords" - умолчание для демонстрации
            cmb.SelectionChanged += (o, e) => UpdateHint();
            root.Children.Add(cmb);

            root.Children.Add(new TextBlock { Text = "Новое имя:", Foreground = FgMain, FontSize = 13 });
            txt = new TextBox { Height = 30, Margin = new Thickness(0, 4, 0, 4), MaxLength = 10, Text = "02601" };
            txt.TextChanged += (o, e) => UpdateHint();
            root.Children.Add(txt);
            hint = new TextBlock { Foreground = FgDim, FontSize = 11, Margin = new Thickness(0, 0, 0, 10) };
            root.Children.Add(hint);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Mk("Применить (UAC)", Accent, Brushes.Black, Apply_Click, 150));
            row.Children.Add(Mk("Вернуть оригинал «Часов»", BgControl, FgMain, Restore_Click, 200));
            root.Children.Add(row);

            status = new TextBlock { Foreground = FgDim, FontSize = 12, Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap };
            root.Children.Add(status);

            Content = root;
            UpdateHint();
        }

        Button Mk(string text, Brush bg, Brush fg, RoutedEventHandler onClick, double width)
        {
            var b = new Button {
                Content = text, Width = width, Height = 34, Margin = new Thickness(0, 0, 10, 0),
                Foreground = fg, Background = bg, BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            b.Click += onClick;
            return b;
        }

        void UpdateHint()
        {
            int slot = cmb.SelectedIndex + 1;
            int max = MaxBytes[slot - 1];
            int used = string.IsNullOrEmpty(txt.Text) ? 0 : Encoding.UTF8.GetByteCount(txt.Text);
            hint.Text = string.Format("Максимум {0} байт UTF-8 у этого слота (кириллица = 2 байта/символ, цифры/латиница = 1). Введено: {1}.", max, used);
            hint.Foreground = (used > max) ? FgRed : FgDim;
        }

        void RunPhase(int phase, string stateFile, string renamesJson, string resultFile)
        {
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "deploy-clock.ps1");
            var psi = new ProcessStartInfo {
                FileName = "powershell.exe",
                Arguments = string.Format(
                    "-NoProfile -ExecutionPolicy Bypass -File \"{0}\" -Phase {1} -StateFile \"{2}\" -RenamesJson \"{3}\" -ResultFile \"{4}\"",
                    script, phase, stateFile, renamesJson, resultFile),
                UseShellExecute = true, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden
            };
            if (phase == 2) { psi.Verb = "runas"; }
            var p = Process.Start(psi);
            p.WaitForExit();
        }

        void Apply_Click(object sender, RoutedEventArgs e)
        {
            int slot = cmb.SelectedIndex + 1;
            int max = MaxBytes[slot - 1];
            string name = txt.Text.Trim();
            if (string.IsNullOrEmpty(name)) { SetStatus("Введите имя.", FgDim); return; }
            if (Encoding.UTF8.GetByteCount(name) > max) { SetStatus("Имя слишком длинное для этого слота.", FgRed); return; }

            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlarmSoundStudio");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string stateFile = Path.Combine(dir, "deploy-state.json");
            string renamesFile = Path.Combine(dir, "deploy-renames.json");
            string resultFile = Path.Combine(dir, "deploy-result.json");
            string workRoot = Path.Combine(dir, "mod");

            File.WriteAllText(stateFile, "{\"workroot\":\"" + workRoot.Replace("\\", "\\\\") + "\",\"version\":\"\",\"work\":\"\"}", Encoding.UTF8);
            File.WriteAllText(renamesFile, "{\"" + slot + "\":\"" + name.Replace("\"", "") + "\"}", Encoding.UTF8);
            if (File.Exists(resultFile)) File.Delete(resultFile);

            SetStatus("Фаза 1/3: копирование и патч пакета…", FgMain);
            try { RunPhase(1, stateFile, renamesFile, resultFile); }
            catch (Exception ex) { SetStatus("Фаза 1: " + ex.Message, FgRed); return; }
            if (!CheckResult(resultFile)) return;

            SetStatus("Фаза 2/3: снятие оригинала (подтвердите UAC)…", FgMain);
            try { RunPhase(2, stateFile, renamesFile, resultFile); }
            catch { SetStatus("UAC-запрос отклонён.", FgRed); return; }
            if (!CheckResult(resultFile)) return;

            SetStatus("Фаза 3/3: регистрация и запуск…", FgMain);
            try { RunPhase(3, stateFile, renamesFile, resultFile); }
            catch (Exception ex) { SetStatus("Фаза 3: " + ex.Message, FgRed); return; }
            if (!CheckResult(resultFile)) return;

            SetStatus("Готово! Перезапустите «Часы», откройте будильник → список звуков — там ваше имя. Звук слота играет ваш файл.", FgGreen);
        }

        void Restore_Click(object sender, RoutedEventArgs e)
        {
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlarmSoundStudio");
            string resultFile = Path.Combine(dir, "deploy-result.json");
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "restore-clock.ps1");
            if (!File.Exists(script)) { SetStatus("restore-clock.ps1 не найден.", FgRed); return; }
            if (File.Exists(resultFile)) File.Delete(resultFile);
            var psi = new ProcessStartInfo {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -ResultFile \"" + resultFile + "\"",
                UseShellExecute = true, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, Verb = "runas"
            };
            try { Process.Start(psi).WaitForExit(); } catch { SetStatus("UAC-запрос отклонён.", FgRed); return; }
            SetStatus(File.Exists(resultFile) && File.ReadAllText(resultFile).Contains("\"ok\"")
                ? "Оригинальные «Часы» восстановлены из бэкапа/Store."
                : "Не удалось восстановить автоматически — переустановите «Часы» из Microsoft Store.", FgDim);
        }

        bool CheckResult(string resultFile)
        {
            for (int i = 0; i < 10 && !File.Exists(resultFile); i++) System.Threading.Thread.Sleep(200);
            if (!File.Exists(resultFile)) { SetStatus("Операция не завершилась (нет файла результата).", FgRed); return false; }
            string res = File.ReadAllText(resultFile);
            if (res.Contains("\"status\":\"ok\"")) return true;
            string msg = "неизвестная ошибка";
            var m2 = System.Text.RegularExpressions.Regex.Match(res, "\"message\":\"(.*)\"\\}");
            if (m2.Success) msg = m2.Groups[1].Value;
            SetStatus("Ошибка: " + msg, FgRed);
            return false;
        }

        void SetStatus(string t, Brush c) { status.Text = t; status.Foreground = c; }
    }
}
