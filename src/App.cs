// AlarmSoundStudio v1.3 - свои звуки будильника для приложения Windows "Часы".
// + Оверлей поверх окна "Часов" (как в читах): список слотов, ▶, назначение.
// + Селектор языка (RU/EN), хранение WAV в LocalState "Часов" (чтобы ▶ и будильник играли),
//   динамическое обнаружение слотов, мод имён звуков, откат. C# 5, WPF, системный csc.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Markup;
using System.Windows.Threading;
using Microsoft.Win32;

namespace AlarmSoundStudio
{
    // ==================== настройки/язык ====================

    public static class Cfg
    {
        public static string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlarmSoundStudio");
        public static string Lang = "ru";

        public static void Load()
        {
            try {
                var f = Path.Combine(Dir, "config.txt");
                if (!File.Exists(f)) return;
                foreach (var line in File.ReadAllLines(f)) {
                    if (line.StartsWith("lang=", StringComparison.OrdinalIgnoreCase))
                        Lang = line.Substring(5).Trim().ToLowerInvariant() == "en" ? "en" : "ru";
                }
            } catch { }
        }

        public static void Save()
        {
            try {
                if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
                File.WriteAllText(Path.Combine(Dir, "config.txt"), "lang=" + Lang, Encoding.UTF8);
            } catch { }
        }
    }

    public static class L
    {
        static Dictionary<string, string> ru = new Dictionary<string, string>();
        static Dictionary<string, string> en = new Dictionary<string, string>();

        static L()
        {
            ru["title"] = "AlarmSoundStudio — свои звуки будильника для «Часов»";
            ru["hdr"] = "Мои звуки будильника";
            ru["hdrSub"] = "Слоты обнаружены автоматически в вашей системе (события Windows Notification.Looping.Alarm*).\nВыбранный файл конвертируется в WAV и назначается на событие — приложение «Часы» играет его.";
            ru["add"] = "Добавить звук…";
            ru["apply"] = "Применить";
            ru["play"] = "▶";
            ru["stop"] = "■";
            ru["mod"] = "Имя звука в «Часах»…";
            ru["reset"] = "Сбросить всё";
            ru["overlay"] = "Оверлей (F9)";
            ru["allSlots"] = "На все слоты";
            ru["slotFmt"] = "Слот {0}";
            ru["std"] = "стандартный";
            ru["custom"] = "★ СВОЙ ЗВУК";
            ru["notSet"] = "(не задано)";
            ru["ready"] = "Готово. Выберите аудиофайл (mp3, wav, m4a, ogg и т.д.) и нажмите «Применить».";
            ru["dlgTitle"] = "Выберите аудиофайл для будильника";
            ru["filterAll"] = "Все аудиофайлы|*.mp3;*.wav;*.m4a;*.m4b;*.aac;*.wma;*.mp4;*.3gp;*.amr;*.adt;*.ogg;*.flac;*.opus|MP3|*.mp3|WAV|*.wav|Все файлы|*.*";
            ru["chosen"] = "Выбрано: {0}  —  нажмите «Применить».";
            ru["converting"] = "Конвертация…";
            ru["done"] = "Готово: назначено слотов — {0}. Файл: {1}{2}";
            ru["shortcut"] = "  |  Ярлык создан в меню «Пуск».";
            ru["errNoFile"] = "Сначала выберите файл («Добавить звук…»).";
            ru["errNoSlots"] = "Системные события будильника не найдены.";
            ru["errFmt"] = "Ошибка: {0}";
            ru["resetDone"] = "Пользовательские переназначения удалены — система вернулась к своим стандартным звукам.";
            ru["playing"] = "Играет: {0}";
            ru["stopped"] = "Остановлено.";
            ru["fileMissing"] = "Файл слота не найден: {0}";
            ru["noSlotSel"] = "Слот не выбран.";
            ru["errPlay"] = "Ошибка воспроизведения: {0}";
            ru["fileMissing2"] = "Файл не найден: {0}";
            ru["engineMissing"] = "Не найден tools\\audio-engine.ps1 (должен лежать рядом с exe)";
            ru["convertFail"] = "Конвертация не удалась. {0}";
            ru["notWav"] = "Результат не является корректным WAV (PCM). Формат может не поддерживаться кодеками системы — попробуйте mp3 или wav.";
            ru["lang"] = "Язык:";
            ru["ovTitle"] = "⚡ AlarmSoundStudio";
            ru["ovAdd"] = "＋ Звук";
            ru["ovApply"] = "Применить";
            ru["ovTarget"] = "Слот:";
            ru["ovStatusReady"] = "F9 — показать/скрыть. Выберите слот и жмите ▶.";
            ru["ovNoClock"] = "«Часы» не запущены — оверлей ждёт…";
            ru["ovAssigned"] = "→ слот {0}";
            ru["modTitle"] = "Своё имя звука в списке «Часов»";
            ru["modIntro"] = "Переопределяет приложение «Часы» модифицированной копией, в которой\nзвук выбранного слота называется так, как вы введёте. Требуется UAC (1 раз).\nБудильники и настройки сохраняются. Откат: «Вернуть оригинал».\nПримечание: пока активен мод, интерфейс «Часов» — английский.";
            ru["modSlot"] = "Слот (позиция в списке «Часов»):";
            ru["modName"] = "Новое имя:";
            ru["modApply"] = "Применить (UAC)";
            ru["modRestore"] = "Вернуть оригинал «Часов»";
            ru["modHint"] = "Максимум {0} байт UTF-8 у этого слота (кириллица = 2 байта/символ, цифры/латиница = 1). Введено: {1}.";
            ru["modP1"] = "Фаза 1/3: копирование и патч пакета…";
            ru["modP2"] = "Фаза 2/3: снятие оригинала (подтвердите UAC)…";
            ru["modP3"] = "Фаза 3/3: регистрация и запуск…";
            ru["modDone"] = "Готово! «Часы» перезапущены — звук выбранного слота называется по-вашему и играет ваш файл.";
            ru["modErrUac"] = "UAC-запрос отклонён.";
            ru["modErrName"] = "Введите имя.";
            ru["modErrLong"] = "Имя слишком длинное для этого слота.";
            ru["modErrNoResult"] = "Операция не завершилась (нет файла результата).";
            ru["restoreOk"] = "Оригинальные «Часы» восстановлены.";
            ru["restoreFail"] = "Не удалось восстановить автоматически — переустановите «Часы» из Microsoft Store.";
            ru["restoreMissing"] = "restore-clock.ps1 не найден.";

            en["title"] = "AlarmSoundStudio — custom alarm sounds for the Windows Clock app";
            en["hdr"] = "My alarm sounds";
            en["hdrSub"] = "Slots are discovered automatically in your system (Windows events Notification.Looping.Alarm*).\nThe selected file is converted to WAV and assigned to the event — the Clock app plays it.";
            en["add"] = "Add sound…";
            en["apply"] = "Apply";
            en["play"] = "▶";
            en["stop"] = "■";
            en["mod"] = "Sound name in Clock…";
            en["reset"] = "Reset all";
            en["overlay"] = "Overlay (F9)";
            en["allSlots"] = "All slots";
            en["slotFmt"] = "Slot {0}";
            en["std"] = "default";
            en["custom"] = "★ CUSTOM";
            en["notSet"] = "(not set)";
            en["ready"] = "Ready. Pick an audio file (mp3, wav, m4a, ogg, etc.) and click Apply.";
            en["dlgTitle"] = "Choose an audio file for the alarm";
            en["filterAll"] = "All audio files|*.mp3;*.wav;*.m4a;*.m4b;*.aac;*.wma;*.mp4;*.3gp;*.amr;*.adt;*.ogg;*.flac;*.opus|MP3|*.mp3|WAV|*.wav|All files|*.*";
            en["chosen"] = "Selected: {0}  —  click Apply.";
            en["converting"] = "Converting…";
            en["done"] = "Done: {0} slot(s) assigned. File: {1}{2}";
            en["shortcut"] = "  |  Start Menu shortcut created.";
            en["errNoFile"] = "Pick a file first (Add sound…).";
            en["errNoSlots"] = "No alarm sound events found on this system.";
            en["errFmt"] = "Error: {0}";
            en["resetDone"] = "Custom overrides removed — the system is back to its own default sounds.";
            en["playing"] = "Playing: {0}";
            en["stopped"] = "Stopped.";
            en["fileMissing"] = "Slot file not found: {0}";
            en["noSlotSel"] = "No slot selected.";
            en["errPlay"] = "Playback error: {0}";
            en["fileMissing2"] = "File not found: {0}";
            en["engineMissing"] = "tools\\audio-engine.ps1 not found (must sit next to the exe)";
            en["convertFail"] = "Conversion failed. {0}";
            en["notWav"] = "The result is not a valid PCM WAV. The format may be unsupported — try mp3 or wav.";
            en["lang"] = "Language:";
            en["ovTitle"] = "⚡ AlarmSoundStudio";
            en["ovAdd"] = "＋ Sound";
            en["ovApply"] = "Apply";
            en["ovTarget"] = "Slot:";
            en["ovStatusReady"] = "F9 — show/hide. Pick a slot and hit ▶.";
            en["ovNoClock"] = "Clock is not running — overlay waiting…";
            en["ovAssigned"] = "→ slot {0}";
            en["modTitle"] = "Custom sound name in the Clock list";
            en["modIntro"] = "Overrides the Clock app with a modified copy where the chosen slot is renamed\nto whatever you type. Requires UAC once. Alarms and settings are preserved.\nRollback: Restore original. Note: the Clock UI becomes English while the mod is active.";
            en["modSlot"] = "Slot (position in the Clock list):";
            en["modName"] = "New name:";
            en["modApply"] = "Apply (UAC)";
            en["modRestore"] = "Restore original Clock";
            en["modHint"] = "Max {0} UTF-8 bytes for this slot (Cyrillic = 2 bytes/char, digits/Latin = 1). Entered: {1}.";
            en["modP1"] = "Phase 1/3: copying and patching the package…";
            en["modP2"] = "Phase 2/3: removing the original (confirm UAC)…";
            en["modP3"] = "Phase 3/3: registering and launching…";
            en["modDone"] = "Done! The Clock app restarts — the chosen slot is renamed and plays your file.";
            en["modErrUac"] = "UAC prompt declined.";
            en["modErrName"] = "Enter a name.";
            en["modErrLong"] = "Name is too long for this slot.";
            en["modErrNoResult"] = "Operation did not finish (no result file).";
            en["restoreOk"] = "Original Clock restored.";
            en["restoreFail"] = "Automatic restore failed — reinstall the Clock app from Microsoft Store.";
            en["restoreMissing"] = "restore-clock.ps1 not found.";
        }

        public static string T(string key)
        {
            if (Cfg.Lang == "en") { string v; if (en.TryGetValue(key, out v)) return v; }
            string r;
            if (ru.TryGetValue(key, out r)) return r;
            return key;
        }
    }

    // ==================== слоты и реестр ====================

    public class SlotInfo
    {
        public int Index;
        public string Event;
        public string Default;
    }

    public static class Core
    {
        static readonly string EventsRoot = @"AppEvents\Schemes\Apps\.Default\";
        static readonly string BasePrefix = "Notification.Looping.Alarm";
        public static readonly string MirrorDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AlarmSoundStudio");

        static SlotInfo[] slots;

        public static SlotInfo[] GetSlots()
        {
            if (slots != null) return slots;
            var events = new List<string>();
            try {
                if (Registry.LocalMachine.OpenSubKey(EventsRoot + BasePrefix) != null ||
                    Registry.CurrentUser.OpenSubKey(EventsRoot + BasePrefix) != null) events.Add(BasePrefix);
                for (int n = 2; n <= 10; n++) {
                    string name = BasePrefix + n;
                    if (Registry.LocalMachine.OpenSubKey(EventsRoot + name) != null ||
                        Registry.CurrentUser.OpenSubKey(EventsRoot + name) != null) events.Add(name);
                }
            } catch { }
            try {
                foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser }) {
                    using (var scheme = hive.OpenSubKey(EventsRoot)) {
                        if (scheme == null) continue;
                        foreach (var name in scheme.GetSubKeyNames()) {
                            if (IsAlarmEvent(name) && !events.Contains(name, StringComparer.OrdinalIgnoreCase)) events.Add(name);
                        }
                    }
                }
            } catch { }
            events.Sort(Compare);
            var list = new List<SlotInfo>();
            int idx = 1;
            foreach (var ev in events) {
                list.Add(new SlotInfo { Index = idx++, Event = ev, Default = MachineDefault(ev) });
            }
            slots = list.ToArray();
            return slots;
        }

        static bool IsAlarmEvent(string name)
        {
            if (string.Equals(name, BasePrefix, StringComparison.OrdinalIgnoreCase)) return true;
            if (!name.StartsWith(BasePrefix, StringComparison.OrdinalIgnoreCase)) return false;
            int n;
            return int.TryParse(name.Substring(BasePrefix.Length), out n) && n >= 1;
        }

        static int Compare(string a, string b)
        {
            return NumOf(a).CompareTo(NumOf(b));
        }

        static int NumOf(string eventName)
        {
            var tail = eventName.Substring(BasePrefix.Length);
            int n;
            if (!int.TryParse(tail, out n)) return 1;
            return n;
        }

        static string MachineDefault(string ev)
        {
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser }) {
                using (var k = hive.OpenSubKey(EventsRoot + ev + @"\.Default")) {
                    var v = k == null ? null : k.GetValue(null) as string;
                    if (!string.IsNullOrEmpty(v)) return v;
                }
            }
            return null;
        }

        public static string GetRawCurrent(string ev)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(EventsRoot + ev + @"\.Current"))
                return k == null ? null : k.GetValue(null) as string;
        }

        public static string GetEffectiveSound(string ev)
        {
            var raw = GetRawCurrent(ev);
            if (!string.IsNullOrEmpty(raw)) return raw;
            return MachineDefault(ev);
        }

        public static bool IsUserOverridden(string ev)
        {
            var v = GetRawCurrent(ev);
            return !string.IsNullOrEmpty(v) &&
                   !string.Equals(v, MachineDefault(ev), StringComparison.OrdinalIgnoreCase);
        }

        public static void SetSlotSound(string ev, string wav)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(EventsRoot + ev + @"\.Current"))
                k.SetValue(null, wav);
        }

        public static void ResetSlot(string ev)
        {
            using (var k = Registry.CurrentUser.OpenSubKey(EventsRoot + ev + @"\.Current", true)) {
                if (k != null) {
                    try { k.DeleteValue(null, false); } catch { }
                    try { k.Close(); } catch { }
                }
            }
            try {
                var parent = Registry.CurrentUser.OpenSubKey(EventsRoot + ev, true);
                if (parent != null && parent.GetSubKeyNames().Length == 0) parent.DeleteSubKey(".Current", false);
                if (parent != null) parent.Close();
            } catch { }
        }

        // ---------- хранилище WAV: LocalState "Часов" (гарантированно читается приложением) ----------

        public static string LocalStoreDir()
        {
            try {
                var pkgs = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages");
                foreach (var d in Directory.GetDirectories(pkgs, "Microsoft.WindowsAlarms_*"))
                    return Path.Combine(d, "LocalState", "AlarmSoundStudio");
            } catch { }
            return null;
        }

        static void GrantAcl(string path)
        {
            try {
                var psi = new ProcessStartInfo {
                    FileName = "icacls",
                    Arguments = "\"" + path + "\" /grant *S-1-15-2-1:(OI)(CI)(RX) /grant *S-1-15-2-2:(OI)(CI)(RX)",
                    UseShellExecute = false, CreateNoWindow = true
                };
                using (var p = Process.Start(psi)) p.WaitForExit();
            } catch { }
        }

        // ---------- конвертация ----------

        public static bool IsWav(string path)
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
                        return fmt == 1 || fmt == 0xFFFE;
                    }
                    if (id == "data") return false;
                    pos += 8 + sz + (sz % 2);
                }
                return false;
            } catch { return false; }
        }

        static void ConvertViaEngine(string src, string dst)
        {
            string engine = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "audio-engine.ps1");
            if (!File.Exists(engine)) throw new Exception(L.T("engineMissing"));
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
                        : L.T("convertFail") + " " + (errOut ?? "").Trim());
            }
        }

        /// <summary>Конвертирует/копирует файл в хранилище, возвращает путь, который гарантированно
        /// читается приложением "Часы" (LocalState пакета). Зеркал также в %APPDATA%.</summary>
        public static string ResolveAudio(string src)
        {
            if (!File.Exists(src)) throw new Exception(string.Format(L.T("fileMissing2"), src));
            if (!Directory.Exists(MirrorDir)) Directory.CreateDirectory(MirrorDir);
            string baseName = Path.GetFileNameWithoutExtension(src) + ".wav";
            string mirrorPath = Path.Combine(MirrorDir, baseName);
            string ext = Path.GetExtension(src).ToLowerInvariant();
            if (ext == ".wav" && IsWav(src)) {
                File.Copy(src, mirrorPath, true);
            } else {
                ConvertViaEngine(src, mirrorPath);
            }
            if (!IsWav(mirrorPath)) throw new Exception(L.T("notWav"));
            GrantAcl(MirrorDir);
            GrantAcl(mirrorPath);

            string local = LocalStoreDir();
            if (local != null) {
                if (!Directory.Exists(local)) Directory.CreateDirectory(local);
                GrantAcl(local);
                string dst = Path.Combine(local, baseName);
                File.Copy(mirrorPath, dst, true);
                GrantAcl(dst);
                return dst;
            }
            return mirrorPath;
        }

        /// <summary>Разовая миграция: старые пути (%APPDATA%) -> LocalState.</summary>
        public static void MigratePaths()
        {
            string local = LocalStoreDir();
            if (local == null) return;
            if (!Directory.Exists(local)) Directory.CreateDirectory(local);
            foreach (var s in GetSlots()) {
                string cur = GetRawCurrent(s.Event);
                if (string.IsNullOrEmpty(cur)) continue;
                string fileName = null;
                if (cur.IndexOf(MirrorDir, StringComparison.OrdinalIgnoreCase) >= 0) fileName = Path.GetFileName(cur);
                else if (cur.IndexOf("\\LocalState\\AlarmSoundStudio\\", StringComparison.OrdinalIgnoreCase) >= 0 && !File.Exists(cur))
                    fileName = Path.GetFileName(cur);
                if (fileName == null) continue;
                string srcMirror = Path.Combine(MirrorDir, fileName);
                if (!File.Exists(srcMirror)) continue;
                string dst = Path.Combine(local, fileName);
                try { File.Copy(srcMirror, dst, true); } catch { continue; }
                SetSlotSound(s.Event, dst);
            }
        }

        public static bool EnsureStartMenuShortcut()
        {
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
                sc.Description = "AlarmSoundStudio";
                sc.Save();
                return true;
            } catch { return false; }
        }
    }

    // ==================== главное окно ====================

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
            Cfg.Load();
            if (string.IsNullOrEmpty(Cfg.Lang))
                Cfg.Lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
            var app = new Application { ShutdownMode = ShutdownMode.OnLastWindowClose };
            app.Run(new MainWindow());
        }
    }

    // ==================== тёмные стили контролов ====================

    public static class UiFx
    {
        const string ComboTplXaml = @"
<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
                 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
                 TargetType='ComboBox'>
  <Grid>
    <ToggleButton Focusable='False' ClickMode='Press'
                  IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
      <ToggleButton.Template>
        <ControlTemplate TargetType='ToggleButton'>
          <Border x:Name='bd' Background='#33333F' CornerRadius='8'/>
          <ControlTemplate.Triggers>
            <Trigger Property='IsMouseOver' Value='True'>
              <Setter TargetName='bd' Property='Background' Value='#404052'/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
      </ToggleButton.Template>
    </ToggleButton>
    <ContentPresenter Content='{TemplateBinding SelectionBoxItem}'
                      ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                      ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'
                      Margin='10,0,26,0' VerticalAlignment='Center' HorizontalAlignment='Left'
                      IsHitTestVisible='False'/>
    <Path Data='M 0 0 L 4 4 L 8 0 Z' Fill='#9A9AA8' HorizontalAlignment='Right'
          VerticalAlignment='Center' Margin='0,0,10,0' IsHitTestVisible='False'/>
    <Popup x:Name='PART_Popup' AllowsTransparency='True' Focusable='False' StaysOpen='True'
           Placement='Bottom' IsOpen='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}}'>
      <Border Background='#2A2A36' BorderBrush='#4CC2FF' BorderThickness='1' CornerRadius='8'
              MinWidth='{TemplateBinding ActualWidth}' MaxHeight='220' Margin='0,2,0,0'>
        <ScrollViewer>
          <ItemsPresenter/>
        </ScrollViewer>
      </Border>
    </Popup>
  </Grid>
</ControlTemplate>";

        const string ComboItemXaml = @"
<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
       TargetType='ComboBoxItem'>
  <Setter Property='Foreground' Value='#ECECEC'/>
  <Setter Property='Template'>
    <Setter.Value>
      <ControlTemplate TargetType='ComboBoxItem'>
        <Border x:Name='bd' Background='Transparent' Padding='10,6'>
          <ContentPresenter/>
        </Border>
        <ControlTemplate.Triggers>
          <Trigger Property='IsHighlighted' Value='True'>
            <Setter TargetName='bd' Property='Background' Value='#404052'/>
          </Trigger>
          <Trigger Property='IsSelected' Value='True'>
            <Setter TargetName='bd' Property='Background' Value='#2A6E8F'/>
          </Trigger>
        </ControlTemplate.Triggers>
      </ControlTemplate>
    </Setter.Value>
  </Setter>
</Style>";

        static ControlTemplate comboTpl;
        static Style comboItemStyle;

        public static void StyleCombo(ComboBox c)
        {
            try {
                if (comboTpl == null) comboTpl = (ControlTemplate)XamlReader.Parse(ComboTplXaml);
                if (comboItemStyle == null) comboItemStyle = (Style)XamlReader.Parse(ComboItemXaml);
                c.Template = comboTpl;
                c.ItemContainerStyle = comboItemStyle;
            } catch { }
        }

        public static void StyleTextBox(TextBox t)
        {
            t.Background = new BrushConverter().ConvertFromString("#33333F") as Brush;
            t.Foreground = new BrushConverter().ConvertFromString("#ECECEC") as Brush;
            t.CaretBrush = new BrushConverter().ConvertFromString("#ECECEC") as Brush;
            t.BorderBrush = new BrushConverter().ConvertFromString("#4CC2FF") as Brush;
        }
    }

    public class MainWindow : Window
    {
        List<CardRefs> cards;
        TextBlock status;
        string chosenFile;
        System.Media.SoundPlayer player = new System.Media.SoundPlayer();
        Button btnAdd, btnApply, btnPlay, btnStop, btnMod, btnReset, btnOverlay;
        ComboBox cmbTarget, cmbLang;
        OverlayWindow overlay;

        Brush BgWin      = B("#1E1E26");
        Brush BgCard     = B("#262630");
        Brush BgCardSel  = B("#30303E");
        Brush BgControl  = B("#33333F");
        Brush Accent     = B("#4CC2FF");
        Brush AccentDim  = B("#2A6E8F");
        Brush FgMain     = B("#ECECEC");
        Brush FgDim      = B("#9A9AA8");
        Brush FgGreen    = B("#5EE39A");
        Brush FgRed      = B("#FF7B7B");

        static Brush B(string hex) { var b = new BrushConverter(); return (Brush)b.ConvertFromString(hex); }

        public MainWindow()
        {
            Title = L.T("title");
            Width = 780; Height = 660;
            MinWidth = 700; MinHeight = 580;
            Background = BgWin;
            FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Core.MigratePaths();

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // заголовок + язык
            var headGrid = new Grid { Margin = new Thickness(20, 16, 20, 4) };
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var headStack = new StackPanel();
            headStack.Children.Add(new TextBlock { Text = L.T("hdr"), FontSize = 24, FontWeight = FontWeights.Bold, Foreground = FgMain });
            headStack.Children.Add(new TextBlock { Text = L.T("hdrSub"), FontSize = 12, Foreground = FgDim, Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(headStack, 0);
            headGrid.Children.Add(headStack);
            var langPanel = new StackPanel {
                Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            langPanel.Children.Add(new TextBlock { Text = L.T("lang"), Foreground = FgDim, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            cmbLang = new ComboBox { Height = 26, Width = 84, FontSize = 12, Background = BgControl, Foreground = FgMain };
            cmbLang.Items.Add("Русский");
            cmbLang.Items.Add("English");
            cmbLang.SelectedIndex = (Cfg.Lang == "en") ? 1 : 0;
            UiFx.StyleCombo(cmbLang);
            cmbLang.SelectionChanged += CmbLang_Changed;
            langPanel.Children.Add(cmbLang);
            Grid.SetColumn(langPanel, 1);
            headGrid.Children.Add(langPanel);
            Grid.SetRow(headGrid, 0);
            root.Children.Add(headGrid);

            var scroll = new ScrollViewer { Margin = new Thickness(20, 8, 20, 8), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var listPanel = new StackPanel();
            cards = new List<CardRefs>();
            var allSlots = Core.GetSlots();
            foreach (var s in allSlots) {
                var card = BuildCard(s);
                listPanel.Children.Add(card.Card);
                cards.Add(card);
            }
            if (allSlots.Length == 0) {
                listPanel.Children.Add(new TextBlock { Text = L.T("errNoSlots"), Foreground = FgRed, FontSize = 14, Margin = new Thickness(8) });
            }
            scroll.Content = listPanel;
            Grid.SetRow(scroll, 2);
            root.Children.Add(scroll);

            var controls = new WrapPanel { Margin = new Thickness(20, 4, 20, 4) };
            btnAdd = MkButton(L.T("add"), Accent, Brushes.Black, BtnAdd_Click, 150);
            cmbTarget = new ComboBox {
                Width = 240, Height = 32, Margin = new Thickness(8, 0, 8, 0),
                Background = BgControl, Foreground = FgMain, VerticalContentAlignment = VerticalAlignment.Center
            };
            cmbTarget.Items.Add(L.T("allSlots"));
            foreach (var s in allSlots) cmbTarget.Items.Add(string.Format(L.T("slotFmt"), s.Index));
            if (allSlots.Length > 0) cmbTarget.SelectedIndex = 0;
            UiFx.StyleCombo(cmbTarget);
            btnApply = MkButton(L.T("apply"), AccentDim, FgMain, BtnApply_Click, 110);
            btnPlay = MkButton(L.T("play"), BgControl, FgMain, BtnPlay_Click, 40);
            btnStop = MkButton(L.T("stop"), BgControl, FgMain, BtnStop_Click, 40);
            btnMod = MkButton(L.T("mod"), BgControl, FgMain, BtnMod_Click, 170);
            btnOverlay = MkButton(L.T("overlay"), BgControl, Accent, BtnOverlay_Click, 110);
            btnReset = MkButton(L.T("reset"), BgControl, FgDim, BtnReset_Click, 110);
            foreach (var c in new UIElement[] { btnAdd, cmbTarget, btnApply, btnPlay, btnStop, btnMod, btnOverlay, btnReset })
                controls.Children.Add(c);
            Grid.SetRow(controls, 3);
            root.Children.Add(controls);

            status = new TextBlock {
                Text = L.T("ready"), Foreground = FgDim, FontSize = 12,
                Margin = new Thickness(20, 4, 20, 12), TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(status, 4);
            root.Children.Add(status);

            Content = root;
            RefreshCards();
        }

        void CmbLang_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (cmbLang == null) return;
            string lang = (cmbLang.SelectedIndex == 1) ? "en" : "ru";
            if (lang == Cfg.Lang) return;
            Cfg.Lang = lang;
            Cfg.Save();
            var w = new MainWindow();
            w.Show();
            Close();
        }

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
            nameCol.Children.Add(new TextBlock { Text = string.Format(L.T("slotFmt"), s.Index), Foreground = FgMain, FontSize = 15, FontWeight = FontWeights.SemiBold });
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
            for (int i = 0; i < Core.GetSlots().Length; i++)
                if (Core.GetSlots()[i].Index == index) { pos = i + 1; break; }
            if (pos > 0 && cmbTarget.Items.Count > pos) cmbTarget.SelectedIndex = pos;
        }

        void RefreshCards()
        {
            foreach (var c in cards) {
                string cur = Core.GetEffectiveSound(c.Slot.Event);
                c.Current.Text = string.IsNullOrEmpty(cur) ? L.T("notSet") : cur;
                if (Core.IsUserOverridden(c.Slot.Event)) {
                    c.State.Text = L.T("custom");
                    c.State.Foreground = FgGreen;
                } else {
                    c.State.Text = L.T("std");
                    c.State.Foreground = FgDim;
                }
            }
        }

        Button MkButton(string text, Brush bg, Brush fg, RoutedEventHandler onClick, double width)
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
                Title = L.T("dlgTitle"),
                Filter = L.T("filterAll")
            };
            if (dlg.ShowDialog() == true) {
                chosenFile = dlg.FileName;
                SetStatus(string.Format(L.T("chosen"), chosenFile), FgMain);
            }
        }

        async void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (Core.GetSlots().Length == 0) { SetStatus(L.T("errNoSlots"), FgRed); return; }
            if (string.IsNullOrEmpty(chosenFile)) { SetStatus(L.T("errNoFile"), FgDim); return; }
            int target = cmbTarget.SelectedIndex;
            btnApply.IsEnabled = false;
            SetStatus(L.T("converting"), FgMain);
            try {
                string wav = await Task.Run(() => Core.ResolveAudio(chosenFile));
                int[] targets = (target <= 0) ? Core.GetSlots().Select(s => s.Index).ToArray() : new[] { target };
                foreach (int i in targets) {
                    var s = Core.GetSlots().FirstOrDefault(x => x.Index == i);
                    if (s != null) Core.SetSlotSound(s.Event, wav);
                }
                RefreshCards();
                bool sc = Core.EnsureStartMenuShortcut();
                SetStatus(string.Format(L.T("done"), targets.Length, wav, sc ? L.T("shortcut") : ""), FgGreen);
            } catch (Exception ex) {
                string m = ex.Message;
                if (ex.InnerException != null) m = ex.InnerException.Message;
                SetStatus(string.Format(L.T("errFmt"), m), FgRed);
            }
            btnApply.IsEnabled = true;
        }

        void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            try {
                int idx = cmbTarget.SelectedIndex;
                SlotInfo s = (idx <= 0 && Core.GetSlots().Length > 0) ? Core.GetSlots()[0] : (idx >= 1 && idx <= Core.GetSlots().Length ? Core.GetSlots()[idx - 1] : null);
                if (s == null) { SetStatus(L.T("noSlotSel"), FgDim); return; }
                string p = Core.GetEffectiveSound(s.Event);
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) { SetStatus(string.Format(L.T("fileMissing"), p), FgDim); return; }
                player.Stop();
                player.SoundLocation = p;
                player.Play();
                SetStatus(string.Format(L.T("playing"), p), FgMain);
            } catch (Exception ex) { SetStatus(string.Format(L.T("errPlay"), ex.Message), FgRed); }
        }

        void BtnStop_Click(object sender, RoutedEventArgs e) { player.Stop(); SetStatus(L.T("stopped"), FgDim); }

        void BtnMod_Click(object sender, RoutedEventArgs e)
        {
            var w = new ModWindow();
            w.Owner = this;
            w.ShowDialog();
        }

        void BtnOverlay_Click(object sender, RoutedEventArgs e)
        {
            if (overlay == null) overlay = new OverlayWindow();
            overlay.Toggle();
        }

        void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            foreach (var s in Core.GetSlots()) Core.ResetSlot(s.Event);
            RefreshCards();
            SetStatus(L.T("resetDone"), FgDim);
        }

        void SetStatus(string text, Brush color)
        {
            status.Text = text;
            status.Foreground = color;
        }
    }

    // ==================== ОВЕРЛЕЙ поверх "Часов" ====================

    public class OverlayWindow : Window
    {
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lp);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int idx, int val);
        delegate bool EnumProc(IntPtr h, IntPtr lp);
        [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }

        const int GWL_EXSTYLE = -20;
        const int WS_EX_NOACTIVATE = 0x08000000;
        const int WM_HOTKEY = 0x0312;
        const int HOTKEY_F9 = 0xB00B;    // просто F9
        const int HOTKEY_CAS = 0xB00C;   // Ctrl+Alt+S

        StackPanel listPanel;
        TextBlock status;
        ComboBox cmb;
        Button btnApply;
        string chosenFile;
        System.Media.SoundPlayer player = new System.Media.SoundPlayer();
        DispatcherTimer follow;
        IntPtr clockHwnd = IntPtr.Zero;
        bool pinned;
        List<string[]> rows = new List<string[]>();   // [slotIndex, name, state]

        Brush BgPanel  = B("#E8151520");
        Brush BgRow    = B("#33202030");
        Brush BgRowSel = B("#552C2C44");
        Brush Accent   = B("#4CC2FF");
        Brush FgMain   = B("#F2F2F6");
        Brush FgDim    = B("#B0B0C0");
        Brush FgGreen  = B("#5EE39A");
        Brush FgRed    = B("#FF7B7B");
        Brush BgControl = B("#44445A");

        static Brush B(string hex) { var b = new BrushConverter(); return (Brush)b.ConvertFromString(hex); }

        public OverlayWindow()
        {
            Title = "AlarmSoundStudio Overlay";
            Width = 330; Height = 470;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.Manual;

            SourceInitialized += (o, e) => {
                var src = (HwndSource)PresentationSource.FromVisual(this);
                var style = GetWindowLong(src.Handle, GWL_EXSTYLE);
                SetWindowLong(src.Handle, GWL_EXSTYLE, style | WS_EX_NOACTIVATE);
                src.AddHook(Hook);
                HotKey.RegisterHotKey(src.Handle, HOTKEY_F9, 0x0u, 0x78u);            // F9
                HotKey.RegisterHotKey(src.Handle, HOTKEY_CAS, 0x2u | 0x1u, 0x53u);   // Ctrl+Alt+S
            };
            Closed += (o, e) => {
                if (follow != null) follow.Stop();
                try {
                    var src2 = (HwndSource)PresentationSource.FromVisual(this);
                    if (src2 != null) {
                        HotKey.UnregisterHotKey(src2.Handle, HOTKEY_F9);
                        HotKey.UnregisterHotKey(src2.Handle, HOTKEY_CAS);
                    }
                } catch { }
            };

            var panel = new Border {
                Background = BgPanel, CornerRadius = new CornerRadius(12),
                BorderBrush = Accent, BorderThickness = new Thickness(1),
                Margin = new Thickness(8)
            };
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // заголовок (за него тащим)
            var header = new Border { Background = B("#22223033"), Padding = new Thickness(10, 8, 10, 8), CornerRadius = new CornerRadius(12, 12, 0, 0) };
            var hgrid = new Grid();
            hgrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hgrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var t = new TextBlock { Text = L.T("ovTitle"), Foreground = FgMain, FontWeight = FontWeights.Bold, FontSize = 13 };
            Grid.SetColumn(t, 0);
            hgrid.Children.Add(t);
            var close = new TextBlock { Text = "✕", Foreground = FgDim, Cursor = Cursors.Hand, Margin = new Thickness(8, 0, 0, 0) };
            close.MouseLeftButtonUp += (o, e) => Toggle();
            Grid.SetColumn(close, 1);
            hgrid.Children.Add(close);
            header.Child = hgrid;
            header.MouseLeftButtonDown += (o, e) => { try { DragMove(); } catch { } };
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var controls = new WrapPanel { Margin = new Thickness(10, 8, 10, 6) };
            var btnAdd = Mk(L.T("ovAdd"), BgControl, FgMain, 96, BtnAdd_Click);
            cmb = new ComboBox { Height = 26, Width = 88, FontSize = 11, Margin = new Thickness(4, 0, 4, 0), Background = BgControl, Foreground = FgMain };
            for (int i = 1; i <= 10; i++) cmb.Items.Add(string.Format(L.T("slotFmt"), i));
            if (Core.GetSlots().Length > 0) cmb.SelectedIndex = 0;
            UiFx.StyleCombo(cmb);
            btnApply = Mk(L.T("ovApply"), Accent, Brushes.Black, 78, BtnApply_Click);
            controls.Children.Add(btnAdd);
            controls.Children.Add(cmb);
            controls.Children.Add(btnApply);
            Grid.SetRow(controls, 1);
            root.Children.Add(controls);

            listPanel = new StackPanel { Margin = new Thickness(8, 2, 8, 2) };
            var lv = new ScrollViewer { Content = listPanel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(lv, 2);
            root.Children.Add(lv);

            status = new TextBlock { Text = L.T("ovStatusReady"), Foreground = FgDim, FontSize = 11, Margin = new Thickness(12, 4, 12, 4), TextWrapping = TextWrapping.Wrap };
            Grid.SetRow(status, 3);
            root.Children.Add(status);

            var footer = new TextBlock { Text = "Ctrl+Alt+S", Foreground = B("#66667777"), FontSize = 10, Margin = new Thickness(12, 0, 12, 6), HorizontalAlignment = HorizontalAlignment.Right };
            Grid.SetRow(footer, 4);
            root.Children.Add(footer);

            panel.Child = root;
            Content = panel;

            BuildList();
            follow = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
            follow.Tick += (o, e) => FollowClock();
            follow.Start();
        }

        Button Mk(string text, Brush bg, Brush fg, double width, RoutedEventHandler onClick)
        {
            var b = new Button {
                Content = text, Width = width, Height = 26, FontSize = 11,
                Foreground = fg, Background = bg, BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 4, 0)
            };
            b.Click += onClick;
            return b;
        }

        void BuildList()
        {
            listPanel.Children.Clear();
            rows.Clear();
            foreach (var s in Core.GetSlots()) {
                var cur = Core.GetEffectiveSound(s.Event);
                string file = string.IsNullOrEmpty(cur) ? "" : Path.GetFileName(cur);
                bool own = Core.IsUserOverridden(s.Event);
                var rowBorder = new Border {
                    Background = BgRow, CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(0, 2, 0, 2), Tag = s.Index
                };
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var left = new StackPanel { Orientation = Orientation.Horizontal };
                left.Children.Add(new TextBlock { Text = s.Index + ".", Foreground = Accent, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
                var col = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                col.Children.Add(new TextBlock { Text = file, Foreground = FgMain, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
                left.Children.Add(col);
                Grid.SetColumn(left, 0);
                g.Children.Add(left);

                var play = new TextBlock { Text = "▶", Foreground = own ? FgGreen : FgDim, FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, Margin = new Thickness(6, 0, 0, 0), Tag = s.Index };
                play.MouseLeftButtonUp += (o, e) => PlaySlot(s.Index);
                Grid.SetColumn(play, 1);
                g.Children.Add(play);

                rowBorder.Child = g;
                rowBorder.MouseLeftButtonUp += (o, e) => {
                    if (s.Index >= 1 && s.Index <= cmb.Items.Count) cmb.SelectedIndex = s.Index - 1;
                };
                listPanel.Children.Add(rowBorder);
            }
        }

        void PlaySlot(int slot)
        {
            try {
                var ev = (slot == 1) ? "Notification.Looping.Alarm" : "Notification.Looping.Alarm" + slot;
                string p = Core.GetEffectiveSound(ev);
                if (string.IsNullOrEmpty(p) || !File.Exists(p)) { status.Text = string.Format(L.T("fileMissing"), p); return; }
                player.Stop();
                player.SoundLocation = p;
                player.Play();
                status.Text = string.Format(L.T("playing"), p);
            } catch (Exception ex) { status.Text = string.Format(L.T("errPlay"), ex.Message); }
        }

        void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Title = L.T("dlgTitle"), Filter = L.T("filterAll") };
            if (dlg.ShowDialog() == true) {
                chosenFile = dlg.FileName;
                status.Text = string.Format(L.T("chosen"), chosenFile);
                status.Foreground = FgMain;
            }
        }

        void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            try {
                if (string.IsNullOrEmpty(chosenFile)) { status.Text = L.T("errNoFile"); return; }
                int slot = (cmb.SelectedIndex >= 0 && cmb.SelectedIndex < 10) ? cmb.SelectedIndex + 1 : 0;
                if (slot <= 0) { status.Text = L.T("noSlotSel"); return; }
                var ev = (slot == 1) ? "Notification.Looping.Alarm" : "Notification.Looping.Alarm" + slot;
                string wav = Core.ResolveAudio(chosenFile);
                Core.SetSlotSound(ev, wav);
                BuildList();
                status.Text = string.Format(L.T("ovAssigned"), slot) + " " + wav;
                status.Foreground = FgGreen;
            } catch (Exception ex) {
                string m = ex.Message; if (ex.InnerException != null) m = ex.InnerException.Message;
                status.Text = string.Format(L.T("errFmt"), m);
                status.Foreground = FgRed;
            }
        }

        // ---- позиционирование за окном "Часов" ----

        IntPtr FindClockWindow()
        {
            var pids = new HashSet<uint>();
            foreach (var p in Process.GetProcessesByName("Time")) pids.Add((uint)p.Id);
            if (pids.Count == 0) return IntPtr.Zero;
            IntPtr found = IntPtr.Zero;
            EnumWindows((h, lp) => {
                uint pid; GetWindowThreadProcessId(h, out pid);
                if (pids.Contains(pid) && IsWindowVisible(h)) {
                    var sb = new StringBuilder(256);
                    GetClassName(h, sb, 256);
                    if (sb.ToString() == "Windows.UI.Core.CoreWindow") { found = h; return false; }
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        void FollowClock()
        {
            clockHwnd = FindClockWindow();
            if (clockHwnd == IntPtr.Zero) {
                if (Visibility == Visibility.Visible) { status.Text = L.T("ovNoClock"); }
                return;
            }
            RECT r;
            if (!GetWindowRect(clockHwnd, out r)) return;
            double left = r.L + 36, top = r.T + 56;
            // не вылезать за экран
            var wa = SystemParameters.WorkArea;
            if (left + Width > wa.Right) left = wa.Right - Width - 4;
            if (top + Height > wa.Bottom) top = wa.Bottom - Height - 4;
            if (left < wa.Left) left = wa.Left + 4;
            if (top < wa.Top) top = wa.Top + 4;
            if (!pinned) { Left = left; Top = top; }
        }

        // ---- hotkey / показ ----

        IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY && (wParam.ToInt32() == HOTKEY_F9 || wParam.ToInt32() == HOTKEY_CAS)) Toggle();
            return IntPtr.Zero;
        }

        public void Toggle()
        {
            if (!IsLoaded) { Show(); BuildList(); FollowClock(); return; }
            if (Visibility == Visibility.Visible) { Visibility = Visibility.Collapsed; }
            else {
                BuildList();
                FollowClock();
                Visibility = Visibility.Visible;
            }
        }
    }

    // ===== interop для hotkey =====
    public static class HotKey
    {
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
    }

    // ==================== окно мода имён ====================

    public class ModWindow : Window
    {
        static readonly string[] EnNames = { "Chimes", "Xylophone", "Chords", "Tap", "Jingle", "Transition", "Descending", "Bounce", "Echo", "Ascending" };
        static readonly int[] MaxBytes = { 6, 9, 6, 3, 6, 10, 10, 6, 4, 9 };

        ComboBox cmb;
        TextBox txt;
        TextBlock hint, status;

        Brush BgWin = B("#1E1E26");
        Brush FgMain = B("#ECECEC");
        Brush FgDim = B("#9A9AA8");
        Brush FgGreen = B("#5EE39A");
        Brush FgRed = B("#FF7B7B");
        Brush Accent = B("#4CC2FF");
        Brush BgControl = B("#33333F");

        static Brush B(string hex) { var b = new BrushConverter(); return (Brush)b.ConvertFromString(hex); }

        public ModWindow()
        {
            Title = L.T("modTitle");
            Width = 580; Height = 420;
            Background = BgWin;
            FontFamily = new FontFamily("Segoe UI");
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new StackPanel { Margin = new Thickness(20) };
            root.Children.Add(new TextBlock {
                Text = L.T("modIntro"), Foreground = FgDim, FontSize = 12,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
            });

            root.Children.Add(new TextBlock { Text = L.T("modSlot"), Foreground = FgMain, FontSize = 13 });
            cmb = new ComboBox { Height = 30, Margin = new Thickness(0, 4, 0, 10) };
            for (int i = 1; i <= 10; i++) cmb.Items.Add(string.Format(L.T("slotFmt"), i) + "  (" + EnNames[i - 1] + ")");
            cmb.SelectedIndex = 2;
            UiFx.StyleCombo(cmb);
            cmb.SelectionChanged += (o, e) => UpdateHint();
            root.Children.Add(cmb);

            root.Children.Add(new TextBlock { Text = L.T("modName"), Foreground = FgMain, FontSize = 13 });
            txt = new TextBox { Height = 30, Margin = new Thickness(0, 4, 0, 4), MaxLength = 10, Text = "02601" };
            UiFx.StyleTextBox(txt);
            txt.TextChanged += (o, e) => UpdateHint();
            root.Children.Add(txt);
            hint = new TextBlock { Foreground = FgDim, FontSize = 11, Margin = new Thickness(0, 0, 0, 10) };
            root.Children.Add(hint);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(Mk(L.T("modApply"), Accent, Brushes.Black, Apply_Click, 150));
            row.Children.Add(Mk(L.T("modRestore"), BgControl, FgMain, Restore_Click, 200));
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
            hint.Text = string.Format(L.T("modHint"), max, used);
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
            if (phase == 2) psi.Verb = "runas";
            var p = Process.Start(psi);
            p.WaitForExit();
        }

        void Apply_Click(object sender, RoutedEventArgs e)
        {
            int slot = cmb.SelectedIndex + 1;
            int max = MaxBytes[slot - 1];
            string name = txt.Text.Trim();
            if (string.IsNullOrEmpty(name)) { SetStatus(L.T("modErrName"), FgDim); return; }
            if (Encoding.UTF8.GetByteCount(name) > max) { SetStatus(L.T("modErrLong"), FgRed); return; }

            string dir = Cfg.Dir;
            string stateFile = Path.Combine(dir, "deploy-state.json");
            string renamesFile = Path.Combine(dir, "deploy-renames.json");
            string resultFile = Path.Combine(dir, "deploy-result.json");
            string workRoot = Path.Combine(dir, "mod");

            File.WriteAllText(stateFile, "{\"workroot\":\"" + workRoot.Replace("\\", "\\\\") + "\",\"version\":\"\",\"work\":\"\"}", Encoding.UTF8);
            File.WriteAllText(renamesFile, "{\"" + slot + "\":\"" + name.Replace("\"", "") + "\"}", Encoding.UTF8);
            if (File.Exists(resultFile)) File.Delete(resultFile);

            SetStatus(L.T("modP1"), FgMain);
            try { RunPhase(1, stateFile, renamesFile, resultFile); }
            catch (Exception ex) { SetStatus("Phase 1: " + ex.Message, FgRed); return; }
            if (!CheckResult(resultFile)) return;

            SetStatus(L.T("modP2"), FgMain);
            try { RunPhase(2, stateFile, renamesFile, resultFile); }
            catch { SetStatus(L.T("modErrUac"), FgRed); return; }
            if (!CheckResult(resultFile)) return;

            SetStatus(L.T("modP3"), FgMain);
            try { RunPhase(3, stateFile, renamesFile, resultFile); }
            catch (Exception ex) { SetStatus("Phase 3: " + ex.Message, FgRed); return; }
            if (!CheckResult(resultFile)) return;

            SetStatus(L.T("modDone"), FgGreen);
        }

        void Restore_Click(object sender, RoutedEventArgs e)
        {
            string dir = Cfg.Dir;
            string resultFile = Path.Combine(dir, "restore-result.json");
            string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "restore-clock.ps1");
            if (!File.Exists(script)) { SetStatus(L.T("restoreMissing"), FgRed); return; }
            if (File.Exists(resultFile)) File.Delete(resultFile);
            var psi = new ProcessStartInfo {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + script + "\" -ResultFile \"" + resultFile + "\"",
                UseShellExecute = true, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, Verb = "runas"
            };
            try { Process.Start(psi).WaitForExit(); } catch { SetStatus(L.T("modErrUac"), FgRed); return; }
            SetStatus(File.Exists(resultFile) && File.ReadAllText(resultFile).Contains("\"ok\"")
                ? L.T("restoreOk") : L.T("restoreFail"), FgDim);
        }

        bool CheckResult(string resultFile)
        {
            for (int i = 0; i < 10 && !File.Exists(resultFile); i++) System.Threading.Thread.Sleep(200);
            if (!File.Exists(resultFile)) { SetStatus(L.T("modErrNoResult"), FgRed); return false; }
            string res = File.ReadAllText(resultFile);
            if (res.Contains("\"status\":\"ok\"")) return true;
            string msg = "?";
            var m2 = System.Text.RegularExpressions.Regex.Match(res, "\"message\":\"(.*)\"\\}");
            if (m2.Success) msg = m2.Groups[1].Value;
            SetStatus(string.Format(L.T("errFmt"), msg), FgRed);
            return false;
        }

        void SetStatus(string t, Brush c) { status.Text = t; status.Foreground = c; }
    }
}
