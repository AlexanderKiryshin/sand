Разбор последней Android-сборки от 9 сентября 2026, 23:35 по Москве.

Файл [2.apk](/E:/myProjects/sand/2.apk) занимает **31 900 195 байт — 30,42 МиБ**. Предыдущий [1.apk](/E:/myProjects/sand/1.apk) занимает 32,89 МиБ. Между этими двумя файлами разница **2,47 МиБ**; это измерение файлов, а не прогноз по размерам исходных PNG.

Подробный список ресурсов находится в [Logs/campaign-visual.log, строка 125272](/E:/myProjects/sand/Logs/campaign-visual.log:125272). Раздел `Used Assets and files from the Resources folder, sorted by uncompressed size` начинается на строке 125283. Unity также сохранила `Library/LastBuild.buildreport`.

Размеры ниже получены из ZIP-каталога самого APK. Столбец «В APK» учитывает сжатие каждого файла.

| Файл внутри APK | До ZIP-сжатия, МиБ | В APK, МиБ |
|---|---:|---:|
| lib/armeabi-v7a/libil2cpp.so — скомпилированный C# и зависимости | 36,09 | **10,51** |
| assets/bin/Data/data.unity3d — основной пакет ресурсов | 7,98 | **7,98** |
| lib/armeabi-v7a/libunity.so — движок Unity | 14,41 | **7,72** |
| assets/bin/Data/Managed/Metadata/global-metadata.dat | 5,90 | 1,70 |
| assets/bin/Data/resources.resource — потоковые ресурсы | 1,43 | 1,43 |
| assets/bin/Data/Resources/unity default resources | 3,58 | 0,76 |
| classes.dex | 0,35 | 0,15 |

В сумме библиотеки занимают **18,27 МиБ**, содержимое `assets` — **11,99 МиБ**. Остальное — около 0,16 МиБ. Пакет содержит одну архитектуру, armeabi-v7a; дублирования библиотек между ARMv7 и ARM64 в этом APK нет. Unity указала в логе `Complete build size 509.1 mb`; этот показатель не равен фактическому размеру APK, который измерен непосредственно по файлу.

Разбивка ресурсов Unity до сжатия пакета:

| Категория | Размер по логу Unity |
|---|---:|
| Текстуры | 18,6 МБ |
| Звуки | 1,4 МБ |
| Прочие ресурсы | 2,0 МБ |
| Шейдеры | 58,9 КБ |
| Все пользовательские ресурсы | 22,2 МБ |

**Все 300 LevelData основной кампании вместе занимают около 667,8 КБ до сжатия** — сумма округлённых значений по файлам из отчёта. Превью PNG уровней в этом списке отсутствуют.

Самые заметные текстуры и проверенные кандидаты на уменьшение:

| Ресурс | Размер до сжатия по Unity | Результат проверки |
|---|---:|---|
| Assets/Resources/UI/BoosterAtlas.png | **6,0 МБ** | Используется текущим интерфейсом. Размер 1254×1254, четыре иконки по 627×627. Размер в отчёте практически равен RGBA32 без текстурного сжатия. Причину выбора формата отдельно не измеряли. |
| Встроенный Splash Screen Unity Logo | 2,7 МБ | Встроенная текстура заставки. |
| Assets/Resources/UI/Boosters/Booster_Magnet.png.tmp.png | **1,0 МБ** | Побайтно совпадает с Booster_Magnet.png. Ссылки на GUID и загрузка по имени в коде не найдены. Подтверждённый лишний экземпляр в сборке. |
| Booster_Magnet.png, Booster_Transparent.png, Booster_Shuffle.png | **3,0 МБ суммарно** | Старый набор. Его загрузчики остались в BusesUiArt, но вызовы этих загрузчиков не найдены; текущий интерфейс использует GameUiSymbols → GameBoosterArt → BoosterAtlas. Кандидат на исключение после удаления старой ветки и проверки экранов магазина/бустеров. |
| Assets/Resources/UI/BusesIcons/Cofffe.png | **1,0 МБ** | Ссылки на GUID и загрузка по имени не найдены. Кандидат на исключение. |
| LiberationSans SDF.asset | 1,0 МБ | Атлас шрифта, используется интерфейсом. |
| Assets/Images/5120e2db-062d-4139-a8b5-9649f8664f4b.png | 1,0 МБ | Есть ссылки в GameScene и BucketView.prefab; простое удаление нарушит эти ссылки. |

Дополнительно в сборке присутствуют побайтные дубликаты `Bus_Icon_2.png` и `Icon_Gold.png` из `Resources/UI/BusesIcons` и `UI_FromBuses/_Ui_Images/Icons`. Размер каждого лишнего экземпляра по логу — 85,5 КБ и 16,8 КБ соответственно. Перед объединением нужно перенаправить ссылки.

Настройки реально выполненной сборки прочитаны из `Library/Bee/Player5f9208a6-inputdata.json`: Development=false, IL2CPP ConfigurationName=Release, Ruleset=Minimal, PerformEngineStripping=true, EnableFullGenericSharing=false. Это релизная сборка с минимальным managed stripping. Среди сохранившихся управляемых сборок есть UIElements, Addressables, Mathematics, Collections, 2D Animation и модули Mirra Prototype/Fallback. Размеры этих DLL нельзя напрямую пересчитывать в вклад каждой библиотеки в libil2cpp.so.

Для следующего шага оптимизации:

1. Исключить подтверждённый временный дубль, проверить и убрать старые иконки и Cofffe. Объём этих пяти ресурсов по Unity — около **5 МБ до сжатия**. Точную экономию APK определит повторная сборка.
2. Проверить атлас бустеров в 512×512 с подходящим Android-форматом. При сохранении RGBA32 это около 1 МиБ вместо 6 МиБ; разрешение отдельной иконки станет 256×256. Это оценка сырых пикселей, качество нужно сравнить в интерфейсе, а размер — по APK.
3. В отдельной проверочной сборке попробовать Managed Stripping Level=Medium. Unity указывает, что Minimal не удаляет пользовательский код, а более сильное удаление требует проверки динамических вызовов и reflection; особенно важны SDK, реклама и сохранения. [Документация Unity 6.3](https://docs.unity3d.com/6000.3/Documentation/Manual/managed-code-stripping-configure.html).
4. Сравнить IL2CPP Code Generation с режимом `Optimize for code size and build time` и измерить размер/FPS. Именно так называется бывший `Faster (smaller) builds` в Unity 6.3. [Изменение названия в Unity 6000.3](https://unity.com/releases/editor/whats-new/6000.3.0f1).

Оптимизации выполнены 10 сентября: новый APK занимает 18,50 МиБ (−39,19%). Измерения и проверки приведены в [отчёте об оптимизации](/E:/myProjects/sand/tasks/build-size-optimization.md).
