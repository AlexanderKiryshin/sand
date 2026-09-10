using System.Collections.Generic;
using MirraGames.SDK.Common;

public static class CampaignText
{
    // Column order follows the four languages already shipped by the game.
    private static readonly Dictionary<string, string[]> Text = new Dictionary<string, string[]>
    {
        { "audio_settings", new[] { "Settings", "Настройки", "Ayarlar", "Einstellungen" } },
        { "audio_sounds", new[] { "Sounds", "Звуки", "Sesler", "Geräusche" } },
        { "audio_music", new[] { "Music", "Музыка", "Müzik", "Musik" } },
        { "audio_on", new[] { "On", "Вкл.", "Açık", "An" } },
        { "audio_off", new[] { "Off", "Выкл.", "Kapalı", "Aus" } },
        { "audio_done", new[] { "Done", "Готово", "Tamam", "Fertig" } },
        { "victory", new[] { "Level complete!", "Победа!", "Seviye tamamlandı!", "Stufe geschafft!" } },
        { "victory_next", new[] { "Next level", "Следующий уровень", "Sonraki seviye", "Nächste Stufe" } },
        { "victory_double", new[] { "Double reward · Watch ad", "Удвоить награду · Реклама", "Ödülü ikiye katla · Reklam", "Doppelte Belohnung · Werbung" } },
        { "debug_skip", new[] { "Skip · TEST", "Пропуск · ТЕСТ", "Atla · TEST", "Überspringen · TEST" } },
        { "level", new[] { "Level", "Уровень", "Seviye", "Stufe" } },
        { "level_short", new[] { "Lv.", "Ур.", "Sv.", "St." } },
        { "levels", new[] { "Levels", "Уровни", "Seviyeler", "Stufen" } },
        { "level_select", new[] { "Select level", "Выбор уровня", "Seviye seç", "Stufe wählen" } },
        { "belt_occupied", new[] { "Occupied: {0}/{1}", "Занято: {0}/{1}", "Dolu: {0}/{1}", "Belegt: {0}/{1}" } },
        { "slot_ad", new[] { "+ Slot · Ad", "+ Место", "+ Yer · Reklam", "+ Platz · Ad" } },
        { "slots_max", new[] { "Maximum", "Максимум", "Maksimum", "Maximum" } },
        { "ad_wait", new[] { "Waiting…", "Ожидаем…", "Bekleniyor…", "Warten…" } },
        { "watch_ad", new[] { "Watch ad", "Смотреть рекламу", "Reklam izle", "Werbung ansehen" } },
        { "lesson_sections", new[] { "Two sections: fill the first color, then the second. Both amounts must reach zero.", "Две секции: сначала соберите первый цвет, затем второй. Заполнить нужно обе секции.", "İki bölme: önce ilk rengi, sonra ikinciyi doldur.", "Zwei Fächer: Fülle erst die erste, dann die zweite Farbe." } },
        { "lesson_keys", new[] { "Fill the golden-key bucket to open the barred buckets.", "Заполните ведро с золотым ключом, чтобы открыть вёдра за воротами.", "Parmaklıkları açmak için altın anahtarlı kovayı doldur.", "Fülle den Eimer mit dem goldenen Schlüssel, um das Gitter zu öffnen." } },
        { "lesson_linked", new[] { "Linked buckets travel together and use two slots. Both must fill before they leave; recalling returns the pair together.", "Связанные вёдра едут парой и занимают два места. Уедут после заполнения обоих; возврат тоже возвращает всю пару.", "Bağlı kovalar iki yer tutar ve birlikte dolar. Geri çağırınca ikisi de döner.", "Verbundene Eimer brauchen zwei Plätze. Beide müssen voll sein; Rückruf holt das Paar zurück." } },
        { "lesson_large", new[] { "A large bucket holds twice as much sand and occupies two belt slots.", "Большое ведро вмещает вдвое больше песка и занимает два места на ленте.", "Büyük kova iki kat kum alır ve iki yer kaplar.", "Ein großer Eimer fasst doppelt so viel Sand und belegt zwei Plätze." } },
        { "lesson_crates", new[] { "Each dispatch opens or closes the crates. When only the covered row remains, the crates disappear.", "Каждая отправка открывает или закрывает ящики. Когда остаётся только этот ряд, ящики убираются.", "Her gönderim kasaları açar veya kapatır. Yalnız örtülü sıra kalınca kasalar kalkar.", "Jeder Zug öffnet oder schließt die Kisten. Bleibt nur diese Reihe übrig, verschwinden die Kisten." } },
        { "lesson_swap", new[] { "The marked pair swaps colors after each dispatch while both stay in the inventory.", "Отмеченная пара меняется цветами после каждой отправки, пока оба ведра остаются в запасе.", "İşaretli çift, ikisi de stoktayken her hamlede renk değiştirir.", "Das markierte Paar tauscht nach jedem Zug die Farben, solange beide im Vorrat sind." } },
        { "lesson_loop", new[] { "The inventory belt moves one place clockwise after each dispatch. Watch the arrows to plan the next choice.", "После каждой отправки лента запаса сдвигает вёдра на одно место по часовой стрелке. Следите за стрелками.", "Her gönderim stok bandını saat yönünde bir yer ilerletir.", "Nach jedem Zug rückt das Vorratsband einen Platz im Uhrzeigersinn weiter." } },
        { "coins", new[] { "coins", "монет", "altın", "Münzen" } },
        { "unavailable", new[] { "Unavailable", "Недоступно", "Kullanılamıyor", "Nicht verfügbar" } },
        { "purchased", new[] { "Purchased", "Куплено", "Satın alındı", "Gekauft" } },
        { "learn", new[] { "HOW TO PLAY", "КАК ИГРАТЬ", "NASIL OYNANIR", "SO SPIELST DU" } },
        { "tutorial_title_1", new[] { "Choose a bucket", "Выберите ведро", "Bir kova seç", "Wähle einen Eimer" } },
        { "tutorial_title_2", new[] { "Follow the sand", "Наблюдайте за песком", "Kumu takip et", "Folge dem Sand" } },
        { "tutorial_title_3", new[] { "Your next move", "Ваш следующий ход", "Sıradaki hamlen", "Dein nächster Zug" } },
        { "ice", new[] { "ICE", "ЛЁД", "BUZ", "EIS" } },
        { "gallery", new[] { "Your collection", "Ваша коллекция", "Koleksiyonun", "Deine Sammlung" } },
        { "tutorial_free", new[] { "Collect every color. Tap a bucket on the belt to send it to the back of the queue.", "Соберите все цвета. Нажмите на ведро на ленте, чтобы вернуть его в конец очереди.", "Tüm renkleri topla. Banttaki kovaya dokunarak sıranın sonuna gönder.", "Sammle alle Farben. Tippe auf einen Eimer auf dem Band, um ihn ans Ende der Warteschlange zu schicken." } },
        { "tutorial_skip", new[] { "Skip tutorial", "Пропустить обучение", "Eğitimi atla", "Tutorial überspringen" } },
        { "tutorial_pick", new[] { "Tap the highlighted bucket. It collects sand of the same color.", "Нажмите на выделенное ведро. Оно собирает песок своего цвета.", "İşaretli kovaya dokun. Kendi rengindeki kumu toplar.", "Tippe auf den markierten Eimer. Er sammelt gleichfarbigen Sand." } },
        { "tutorial_watch", new[] { "Watch the sand flow. Full buckets leave the belt and free a slot.", "Посмотрите, как сыплется песок. Полное ведро уезжает и освобождает место.", "Kumu izle. Dolu kovalar banttan ayrılır ve yer açar.", "Beobachte den Sand. Volle Eimer verlassen das Band und schaffen Platz." } },
        { "tutorial_repeat", new[] { "Choose the next bucket. Collect the whole painting to finish!", "Выберите следующее ведро. Соберите всю картину, чтобы пройти уровень!", "Sıradaki kovayı seç. Bitirmek için tüm resmi topla!", "Wähle den nächsten Eimer. Sammle das ganze Bild ein!" } },
        { "lesson_walls", new[] { "Walls block the way. Clear nearby buckets to open a route around them.", "Стены перекрывают путь. Уберите соседние вёдра, чтобы открыть обход.", "Duvarlar yolu kapatır. Yan kovaları alarak geçit aç.", "Mauern versperren den Weg. Räume Eimer daneben weg, um einen Umweg zu öffnen." } },
        { "lesson_feed", new[] { "A feeder holds extra buckets. Clear the cell in front of its arrow.", "Подающая лента хранит запас вёдер. Освободите ячейку перед стрелкой.", "Besleyici yedek kova tutar. Okun önündeki hücreyi boşalt.", "Der Zubringer hält Eimer bereit. Räume das Feld vor seinem Pfeil frei." } },
        { "lesson_side", new[] { "Side feeders push in the arrow's direction. Keep their exit open.", "Боковая подача идёт по стрелке. Оставляйте выход свободным.", "Yan besleyici ok yönünde iter. Çıkışını açık tut.", "Seitliche Zubringer folgen dem Pfeil. Halte ihren Ausgang frei." } },
        { "lesson_mystery", new[] { "Mystery buckets reveal their color when you open a route to them.", "Цвет ведра со знаком ? откроется, когда вы освободите путь к нему.", "Gizemli kovaların rengi, yolları açılınca görünür.", "Geheime Eimer zeigen ihre Farbe, sobald ein Weg zu ihnen frei ist." } },
        { "lesson_ice", new[] { "Send other buckets to melt the ice. The ice number counts the moves left.", "Отправляйте другие вёдра, чтобы растопить лёд. Число на льду — оставшиеся ходы.", "Buzu eritmek için başka kovaları gönder. Sayı kalan hamleleri gösterir.", "Schicke andere Eimer los, um Eis zu schmelzen. Die Zahl zeigt die übrigen Züge." } },
        { "lesson_slots", new[] { "Only four belt slots. Look at the lowest sand colors before choosing.", "На ленте четыре места. Перед выбором посмотрите на нижние цвета песка.", "Bantta dört yer var. Seçmeden önce alttaki kum renklerine bak.", "Vier Plätze auf dem Band. Achte vor der Wahl auf die untersten Sandfarben." } },
        { "lesson_speed", new[] { "A faster belt! Tap a bucket on the belt to recall it if you need another color.", "Лента стала быстрее! Нажмите на ведро на ленте, чтобы вернуть его, если нужен другой цвет.", "Bant hızlandı! Başka renk gerekiyorsa banttaki kovaya dokun ve geri al.", "Ein schnelleres Band! Tippe auf einen Eimer, um ihn für eine andere Farbe zurückzuholen." } },
        { "lesson_double", new[] { "Two feeders share the board. Alternate their exits to reach both reserves.", "Две подачи на одном поле. Освобождайте оба выхода, чтобы использовать весь запас.", "İki besleyici var. Her iki yedeğe ulaşmak için çıkışları boşalt.", "Zwei Zubringer teilen das Feld. Räume beide Ausgänge frei." } },
        { "lesson_mix", new[] { "Ice and mystery together: melt the ice and open routes to reveal colors.", "Лёд и скрытые цвета вместе: растопите лёд и откройте пути к вёдрам.", "Buz ve gizem birlikte: buzu erit ve renkleri görmek için yolları aç.", "Eis und Geheimnisse: Schmilz das Eis und öffne Wege, um Farben aufzudecken." } },
        { "lesson_challenge", new[] { "Challenge {0} · {1} belt slots and a new route.", "Испытание {0} · Мест на ленте: {1}. Новая схема обхода.", "Görev {0} · Bantta {1} yer ve yeni bir rota.", "Aufgabe {0} · {1} Bandplätze und ein neuer Weg." } },
        { "mystery_short", new[] { "Hidden colors.", "Скрытые цвета.", "Gizli renkler.", "Verborgene Farben." } },
        { "ice_short", new[] { "Frozen buckets.", "Замороженные вёдра.", "Donmuş kovalar.", "Gefrorene Eimer." } },
        { "reserve", new[] { "Reserve", "Запас", "Yedek", "Vorrat" } },
        { "collection", new[] { "Collection complete!", "Коллекция собрана!", "Koleksiyon tamamlandı!", "Sammlung vollständig!" } }
    };

    public static string Get(string key)
    {
        LanguageType language = LocalizationManager.Instance != null ? LocalizationManager.Instance.GetCurrentLanguage() : LanguageType.English;
        int column = language == LanguageType.Russian ? 1 : language == LanguageType.Turkish ? 2 : language == LanguageType.German ? 3 : 0;
        return Text.TryGetValue(key, out string[] values) ? values[column] : key;
    }
}
