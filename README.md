# Zapret Desktop

Простая графическая оболочка для [zapret](https://github.com/bol-van/zapret).
Обходит DPI-блокировки Discord, YouTube, Telegram и других сервисов: выбирает рабочую
стратегию, запускает движок без окна и живёт в системном трее.

Работает на **Windows** (`winws` + WinDivert) и **Linux** (`nfqws` + nftables):
Arch, Debian/Ubuntu, Fedora.

## 📥 Скачать

| Сборка | Для чего |
|---|---|
| [**Установщик (x64)**](https://github.com/HELLAIRSDEV/zapret-desktop/releases/latest/download/ZapretDesktop-Setup-x64.exe) | Обычная установка в Windows, ярлыки, удаление через «Программы и компоненты» |
| [**Portable (x64)**](https://github.com/HELLAIRSDEV/zapret-desktop/releases/latest/download/ZapretDesktop-win-x64.zip) | Распаковал → запустил, без установки |
| [**Portable (ARM64)**](https://github.com/HELLAIRSDEV/zapret-desktop/releases/latest/download/ZapretDesktop-win-arm64.zip) | Для Windows на ARM-процессорах (Snapdragon X и т.п.) |
| [**Arch (pkg.tar.zst)**](https://github.com/HELLAIRSDEV/zapret-desktop/releases/latest) | `sudo pacman -U zapret-desktop-*.pkg.tar.zst` |
| [**Debian / Ubuntu (deb)**](https://github.com/HELLAIRSDEV/zapret-desktop/releases/latest) | `sudo apt install ./zapret-desktop_*_amd64.deb` |
| [**Fedora (rpm)**](https://github.com/HELLAIRSDEV/zapret-desktop/releases/latest) | `sudo dnf install ./zapret-desktop-*.rpm` |
| [**Linux portable (tar.gz)**](https://github.com/HELLAIRSDEV/zapret-desktop/releases/latest) | Распаковал → `./ZapretDesktop` |
| [Все релизы](https://github.com/HELLAIRSDEV/zapret-desktop/releases) | Предыдущие версии |

> Все сборки self-contained — .NET отдельно ставить не нужно.
> Для Linux-версии нужны `nftables` и `polkit` (`pkexec`); при первом включении
> обхода система спросит пароль администратора.

## Возможности

- **Обход DPI** — 11 готовых стратегий (General, ALT, ALT2–ALT4, Simple Fake, Fake + Split2,
  Discord Voice, YouTube) на базе наборов [Flowseal](https://github.com/Flowseal/zapret-discord-youtube).
- **Автообновление стратегий** — при запуске приложение делает лёгкую проверку репозитория
  Flowseal (один условный запрос, без скачивания). Если появился новый коммит — показывает
  предложение «Обновить / Пропустить»; после подтверждения стратегии скачиваются и заменяют
  встроенные, вместе с названиями и аргументами. Проверку можно выключить в настройках.
- **Автоподбор** — приложение по очереди проверяет стратегии на Discord, YouTube, Telegram
  и Google, измеряет отклик и включает лучшую.
- **Telegram** — автоподбор публичных MTProto-прокси в один клик и открытие Telegram по
  ссылке `tg://proxy`. Без ботов и ручного ввода секретов (ручной режим тоже есть).
- **Списки доменов** — редактор списков и ввод в стиле `echo`: написал домен, нажал Enter —
  он сразу записался в `list-general-user.txt`.
- **Трей** — закрытие окна сворачивает приложение в трей; в фоне приложение снижает
  приоритет и освобождает память.
- **Настройки** — автозапуск с Windows, запуск свёрнутым, автозапуск обхода, логи,
  игровой фильтр (порты 1024–65535).

## Linux

Движок на Linux — `nfqws` из состава zapret, трафик заворачивается в NFQUEUE
правилами `nftables`. Бинарь `nfqws` уже входит в пакет, отдельно ставить не нужно.

```bash
# Arch / Manjaro
sudo pacman -U zapret-desktop-1.1.1-1-x86_64.pkg.tar.zst

# Debian / Ubuntu
sudo apt install ./zapret-desktop_1.1.1_amd64.deb

# Fedora
sudo dnf install ./zapret-desktop-1.1.1-1.x86_64.rpm

# Или просто распаковать архив
tar xzf ZapretDesktop-linux-x86_64-v1.1.1.tar.gz
./ZapretDesktop-linux-x86_64/ZapretDesktop
```

Права root нужны только движку: при включении обхода `pkexec` попросит пароль
(через polkit, повторно в течение сессии не спрашивает) — приложение создаёт
правила nftables и запускает `nfqws`, при выключении всё убирает.

Данные в пакетной установке: `~/.local/share/ZapretDesktop/` (списки, конфиг, логи).

## Требования

- Windows 10/11 (x64 или ARM64)
- Права администратора — обязательны для WinDivert. Приложение само запросит UAC.

## Быстрый старт

1. Скачай установщик или portable-архив и запусти `ZapretDesktop.exe` (подтверди UAC).
2. На вкладке **«Обход»** выбери стратегию и нажми **«Запустить»**.
3. Если не работает — открой вкладку **«Автоподбор»** и нажми **«Начать подбор»**:
   приложение само найдёт рабочую стратегию и включит её.
4. Закрой окно — приложение останется в трее, обход продолжит работать.
   Полный выход — правый клик по значку в трее → «Выход».

### Telegram

Открой вкладку **«Telegram»** — список рабочих MTProto-прокси загрузится автоматически.
Нажми **«Подключить в Telegram»** и подтверди прокси в Telegram. Если сервер перестал
работать, нажми **«Подобрать прокси»** ещё раз.

### Свои сайты

Вкладка **«Списки»** → выбери файл (по умолчанию `list-general.txt`, он реально влияет
на обход) или введи домен справа и нажми Enter. Если обход запущен, он перезапустится сам.

## Где лежат данные

- `bin/` — `winws.exe`, WinDivert и `.bin`-файлы фейков;
- `lists/` — списки доменов и IP;
- `config/settings.json` — настройки;
- `config/presets.json` — стратегии и статистика;
- `config/flowseal.json` — скачанные из GitHub стратегии Flowseal и состояние проверки обновлений;
- `logs/` — журналы `zapret-ГГГГ-ММ-ДД.log` и `crash.log`.

## Сборка из исходников

Windows (WPF):

```bash
dotnet publish src/ZapretDesktop.Desktop/ZapretDesktop.Desktop.csproj -c Release -r win-x64 --self-contained
```

Linux (Avalonia):

```bash
# nfqws нужен в src/ZapretDesktop.Linux/scripts/nfqws
make -C zapret/nfq   # из https://github.com/bol-van/zapret
cp zapret/nfq/nfqws src/ZapretDesktop.Linux/scripts/

dotnet publish src/ZapretDesktop.Linux/ZapretDesktop.Linux.csproj -c Release -r linux-x64 --self-contained
```

Пакеты `.deb` / `.rpm` / `pkg.tar.zst` собираются скриптами в `packaging/linux/`;
CI (`.github/workflows/release.yml`) делает это автоматически по тегу `v*`.



```powershell
dotnet build src\ZapretDesktop.sln -c Release
```

Публикация portable-сборок:

```powershell
dotnet publish src\ZapretDesktop.Desktop -c Release -r win-x64 --self-contained true -o publish\win-x64
dotnet publish src\ZapretDesktop.Desktop -c Release -r win-arm64 --self-contained true -o publish\win-arm64
```

Для сборки нужны файлы в `bin/`: `winws.exe`, `WinDivert.dll`, `WinDivert64.sys`,
`cygwin1.dll` и `*.bin` (берутся из [zapret-win-bundle](https://github.com/bol-van/zapret-win-bundle)).

## Структура проекта

```
bin/                          winws и ассеты WinDivert
lists/                        списки доменов и IP
src/ZapretDesktop.Core/       движок, стратегии, автотест, MTProto-прокси, настройки
src/ZapretDesktop.Desktop/    WPF-интерфейс, трей, автозапуск
```

## Решение проблем

- **Стратегии не работают.** У разных провайдеров работают разные наборы — обычно 1–3 из 11.
  Запусти автоподбор; если ничего не помогло — попробуй вручную `ALT`, `ALT2` или `Universal (Fake + Split2)`.
- **Ничего не запускается.** Проверь, что приложение запущено от администратора и в `bin/`
  есть `winws.exe`, `WinDivert.dll`, `WinDivert64.sys`, `cygwin1.dll`.
  в исключения.
- **Подробности.** Смотри `logs/zapret-*.log` — там видны аргументы запуска и результаты
  проверки каждого домена.

## Используемые проекты

- [bol-van/zapret](https://github.com/bol-van/zapret) — движок обхода (GPLv2);
- [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) — наборы стратегий;
- [basil00/WinDivert](https://github.com/basil00/WinDivert) — перехват трафика (LGPL);
- [lepo.co WPF-UI](https://github.com/lepoco/wpfui) — оформление интерфейса (MIT);
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM (MIT).

## Правовая информация

Это неофициальный пользовательский интерфейс. Сетевую часть выполняет сторонний движок
`winws` из проекта [bol-van/zapret](https://github.com/bol-van/zapret) (GPLv2); сборки
содержат его неизменённые бинарные файлы, исходный код движка доступен по ссылке выше.

Собственный код проекта распространяется по лицензии MIT (см. [LICENSE](LICENSE)).
