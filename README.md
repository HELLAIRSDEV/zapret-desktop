# Zapret Desktop

Простая графическая оболочка для [zapret](https://github.com/bol-van/zapret) (`winws`) на Windows.
Обходит DPI-блокировки Discord, YouTube, Telegram и других сервисов: выбирает рабочую
стратегию, запускает `winws` без окна и живёт в системном трее.

## 📥 Скачать

| Сборка | Для чего |
|---|---|
| [**Установщик (x64)**](https://github.com/HELLAIRS/zapret-desktop/releases/latest/download/ZapretDesktop-Setup-x64.exe) | Обычная установка в Windows, ярлыки, удаление через «Программы и компоненты» |
| [**Portable (x64)**](https://github.com/HELLAIRS/zapret-desktop/releases/latest/download/ZapretDesktop-win-x64.zip) | Распаковал → запустил, без установки |
| [**Portable (ARM64)**](https://github.com/HELLAIRS/zapret-desktop/releases/latest/download/ZapretDesktop-win-arm64.zip) | Для Windows на ARM-процессорах (Snapdragon X и т.п.) |
| [Все релизы](https://github.com/HELLAIRS/zapret-desktop/releases) | Предыдущие версии |

> Все сборки self-contained — .NET отдельно ставить не нужно.

## Возможности

- **Обход DPI** — 11 готовых стратегий (General, ALT, ALT2–ALT4, Simple Fake, Fake + Split2,
  Discord Voice, YouTube) на базе наборов [Flowseal](https://github.com/Flowseal/zapret-discord-youtube).
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
- `logs/` — журналы `zapret-ГГГГ-ММ-ДД.log` и `crash.log`.

## Сборка из исходников

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
- **Антивирус ругается на WinDivert.** Это типичный RiskTool-детект; добавь папку приложения
  в исключения.
- **Подробности.** Смотри `logs/zapret-*.log` — там видны аргументы запуска и результаты
  проверки каждого домена.

## Используемые проекты

- [bol-van/zapret](https://github.com/bol-van/zapret) — движок обхода (GPLv2);
- [Flowseal/zapret-discord-youtube](https://github.com/Flowseal/zapret-discord-youtube) — наборы стратегий;
- [basil00/WinDivert](https://github.com/basil00/WinDivert) — перехват трафика (LGPL);
- [lepo.co WPF-UI](https://github.com/lepoco/wpfui) — оформление интерфейса (MIT);
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) — MVVM (MIT).

## Отказ от ответственности

Программа предоставляется «как есть». Автор не несёт ответственности за возможные
последствия использования, включая ограничения со стороны провайдера или законодательства.

