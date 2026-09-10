# Аналитика WebGL

Установлены GameAnalytics 8.1.0 (встроенный UPM-пакет `Packages/com.gameanalytics.sdk`) и официальный модуль Your Games Metrica 1.02. Используется Яндекс.Метрика для браузера. Мобильная AppMetrica не подключается.

В Unity: **Window → Sand → Analytics → Create or Select Web Settings**. Команда создаёт/выбирает настройки и сохраняет введённые ключи.

1. В `Assets/PluginYourGames/Resources/SettingsYG2.asset`, секция **Metrica**, указать **Metrica Counter ID** своего счётчика. Enable и Use Yandex Metrica включены.
2. В `Assets/Resources/GameAnalytics/Settings.asset`, платформа **WebGL**, указать **Game Key** и **Secret Key** из настроек игры GameAnalytics.
3. В счётчике Метрики создать цели типа «JavaScript-событие» с идентификаторами `level_started`, `level_completed`, `level_restarted`, `level_abandoned`, `level_failed`, `tutorial_completed`, `bonus_used`, `video_ads_available`, `video_ads_success`, `resource_changed`, `buy_inapp`. Для воронки прохождения достаточно первых двух. Номер конкретного уровня передаётся параметром `level` (1–300), отдельные 300 целей создавать не нужно.
4. После WebGL-сборки проверить на целевой площадке старт, победу и перезапуск. В GameAnalytics это progression `campaign → level_001…level_300` со статусами Start/Complete/Fail; остальные действия — design-события `gameplay:*`. В Метрике — соответствующая цель и её параметры.

Каждая попытка получает отдельный `attempt_id`, номер попытки, уровень, тему, механики, длительность, количество ходов и бустеров. Повторный callback победы не дублирует прохождение. Перезапуск завершает предыдущую попытку; переключение незаконченного уровня записывает уход. Закрытие вкладки не гарантирует callback Unity, поэтому отток следует также считать по начатым без завершения попыткам. В игре нет автоматического поражения от заполненной ленты; `level_failed` предусмотрен для явного поражения, а отладочный пропуск не считается победой.

MirraSDK продолжает обслуживать платформу, рекламу и сохранения. В Your Games поле Basic/Platform намеренно пустое и отключено автоматическое применение настроек, чтобы второй SDK не запускал платформу повторно и не менял шаблон Mirra.

События буферизуются до готовности каждого сервиса отдельно. Editor, предпросмотр и проверки не отправляют production-события. Без ID/ключей отправка соответствующему сервису не запускается.

Проверены логика попыток, JavaScript-мосты с имитацией SDK и компиляция WebGL-веток C#. В установленном Unity отсутствует WebGL Build Support, поэтому IL2CPP-сборка и поступление событий в реальные кабинеты пока не проверены.

Официальная документация: [Your Games Metrica](https://max-games.ru/plugin-yg/doc/metrica/), [GameAnalytics Unity](https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/).
