using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class LanguageSource
{
    public enum LANGUAGE { ENGLISH, GERMAN };
    public static LANGUAGE current_language;

    private const string GERMAN_TRANSLATION_NEEDED = "GERMAN TRANSLATION NEEDED";

    // JPB: TODO: Needs German translations
    private static Dictionary<string, string[]> language_string_dict = new Dictionary<string, string[]>()
    {
        {"", new string[] {"", ""}},

        { "final recall", new string[] {"All items exhausted. Press any key to proceed to final recall.", "Alle Gegenstände ausgeliefert. Weiter mit beliebiger Taste."} },
        { "final no recall", new string[] {"All items exhausted. Press any key to proceed.", GERMAN_TRANSLATION_NEEDED} },
        { "store cue recall", new string[] {"Please recall which object you delivered to the store shown on the screen.", "Bitte nennen Sie den Gegenstand, den Sie zu dem dargestellten Geschäft geliefert haben."} },
        { "day objects recall", new string[] {"After the beep, please recall all objects from this delivery day.", "Nach dem Piepton, erinnern Sie bitte alle Gegenstände, die Sie in dieser Runde zugestellt haben."} },
        { "microphone test", new string[] {"Microphone Test", "Mikrofontest"} },
        { "next package prompt", new string[] {"The next package has to be delivered to the ", "Als nächstes beliefern Sie "} },
        { "rating improved", new string[] {"Your rating improved!", "Ihre Wertung hat sich verbessert!"} },
        //{ "you now have", new string[] {"You now have points: ", "Aktuelle Punktzahl: "} },
        //{ "you earn points", new string[] {"You earned points: ", "Verdiente Punkte: "} },
        { "continue", new string[] {"Press (X) to continue.", "Drücken Sie (X) um fortzufahren."} },
        { "please point", new string[] {"Please point to the ", "Bitte richten Sie den Pfeil aus auf "} },
        { "joystick", new string[] {"Use the joystick to adjust the arrow, then press (X) to continue.", "Nutzen Sie den Joystick um den Pfeil zu rotieren und (X) um zu bestätigen."} },
        { "keyboard", new string[] {"Use Arrow keys to adjust the arrow, then press (X) to continue", GERMAN_TRANSLATION_NEEDED} },
        { "wrong by", new string[] {"Not quite. The arrow will now show the exact direction. That was off by degrees: ", "Nicht ganz! Der Pfeil zeigt Ihnen nur die richtige Richtung. Abweichung in Grad zur korrekten Antwort: "} },
        { "correct to within", new string[] {"Good! That was correct to within degrees: ", "Fast perfekt! Abweichung in Grad zur korrekten Antwort: "} },
        { "all objects recall", new string[] {"Please recall all the objects that you delivered.", "Bitte erinnern Sie alle Gegenstände, die Sie zugestellt haben."} },
        { "all stores recall", new string[] {"Please recall all the stores that you delivered objects to.", "Bitte erinnern Sie alle Geschäfte, zu denen Sie Pakete geliefert haben."} },
        { "end message", new string[] {"Thank you for being a great delivery person!", "Vielen Dank für Ihre Teilnahme!"} },
        { "end message scored", new string[] {"Thank you for being a great delivery person! Your cumulative score is: ", "Vielen Dank für Ihre Teilnahme! Ihre abschließende Wertung ist: "} },

        { "standard intro video", new string[] {"Press (Y) to continue, \n Press (N) to replay instructional video.",
                                                "Drücken Sie (Y) um fortzufahren, \n Drücken Sie (N) um das Video noch einmal zu sehen."} },
        { "efr intro video", new string[] {"Press (Y) to continue to the next practice delivery day, \n Press (N) to replay instructional video.",
                                           "Drücken Sie (Y) um die nächste trainier Auslieferungsrunde zu starten, \n Drücken Sie (N) um das Video noch einmal zu sehen."} },

        { "nicls movie", new string[] {"Now we will return to the memory task.\nPress (Y) to continue.", GERMAN_TRANSLATION_NEEDED } },

        { "next day", new string[] {"Press (X) to proceed to the next delivery day.", "Drücken Sie (X) um die nächste Auslieferungsrunde zu starten."} },
        { "next practice day", new string[] {"Press (X) to proceed to the next practice delivery day.",
                                             "Drücken Sie (X) um die nächste trainier Auslieferungsrunde zu starten."} },

        { "first day main", new string [] {"Let’s start the first delivery day!", GERMAN_TRANSLATION_NEEDED}},
        { "efr first day description", new string [] {"Don’t forget to continue pressing the left/right buttons when recalling items at the end of each delivery day.",
                                                  GERMAN_TRANSLATION_NEEDED}},

        { "new efr first day description", new string [] {"Don’t forget to continue pressing the (B) button to reject words when recalling items at the end of each delivery day.",
                                                  GERMAN_TRANSLATION_NEEDED}},

        { "frame test start title", new string [] { "Frame Rate Testing ", GERMAN_TRANSLATION_NEEDED} },
        { "frame test start main", new string [] { "First, we will check if your connection is fast enough to complete this task. \nAs a test, please briefly navigate around this town using the arrow keys.", 
                                                   GERMAN_TRANSLATION_NEEDED } },
        { "frame test end title", new string [] { "Your average FPS was ", GERMAN_TRANSLATION_NEEDED} },
        { "frame test end main", new string [] { "If you experienced any significant lag, you will likely take longer than average to complete the task. However, we can only pay a fixed rate for task completion, regardless of time taken." + "\n" +
                                                 "If your connection was strong and you wish to continue, press (X)." + "\n" +
                                                 "Otherwise, close the window to exit the experiment.", GERMAN_TRANSLATION_NEEDED} },
        { "frame test continue main", new string [] {"Thank you for your participation. \n\nTry to focus on the task and good luck!", GERMAN_TRANSLATION_NEEDED} },


        { "town learning title", new string [] { "Town Learning Phase", GERMAN_TRANSLATION_NEEDED } },
        { "town learning main 1", new string [] { "Now let's learn the layout of the town!\nPlease locate all of the stores one by one.", GERMAN_TRANSLATION_NEEDED } },
        { "town learning main 2", new string [] { "Let's do it again!\nPlease locate all of the stores one by one.", GERMAN_TRANSLATION_NEEDED } },

        { "efr left button correct message", new string [] {" Press the <i>left button</i> \nfor correct recall",
                                                            GERMAN_TRANSLATION_NEEDED}},
        { "efr left button incorrect message", new string [] {" Press the <i>left button</i> \nfor incorrect recall",
                                                              GERMAN_TRANSLATION_NEEDED}},
        { "efr right button correct message", new string [] {"Press the <i>right button</i>\nfor correct recall",
                                                             GERMAN_TRANSLATION_NEEDED}},
        { "efr right button incorrect message", new string [] {"Press the <i>right button</i>\nfor incorrect recall",
                                                               GERMAN_TRANSLATION_NEEDED}},

        { "efr keypress practice left button correct message", new string [] {" Press the <b><i>left button</i></b> \nfor correct recall",
                                                                              GERMAN_TRANSLATION_NEEDED}},
        { "efr keypress practice left button incorrect message", new string [] {" Press the <b><i>left button</i></b> \nfor incorrect recall",
                                                                                GERMAN_TRANSLATION_NEEDED}},
        { "efr keypress practice right button correct message", new string [] {"Press the <b><i>right button</i></b>\nfor correct recall",
                                                                               GERMAN_TRANSLATION_NEEDED}},
        { "efr keypress practice right button incorrect message", new string [] {"Press the <b><i>right button</i></b>\nfor incorrect recall",
                                                                                 GERMAN_TRANSLATION_NEEDED}},

        { "practice invitation", new string [] {"Let's practice!", GERMAN_TRANSLATION_NEEDED}},

        { "efr check main", new string [] {"Let's make sure your keys are working.", GERMAN_TRANSLATION_NEEDED}},
        { "efr check description left button", new string [] {"Please press the <i><b>left button</b></i>, and make sure the text on the left is bolded:",
                                                              GERMAN_TRANSLATION_NEEDED}},
        { "efr check description right button", new string [] {"Please press the <i><b>right button</b></i>, and make sure the text on the right is bolded:",
                                                              GERMAN_TRANSLATION_NEEDED}},
        { "efr check try again main", new string [] {"Try again!", GERMAN_TRANSLATION_NEEDED}},
        { "efr check try again description", new string [] {"Make sure you press the designated buttons after saying each word.", GERMAN_TRANSLATION_NEEDED}},

        { "efr keypress practice main", new string [] {"Let's practice pressing the keys.", GERMAN_TRANSLATION_NEEDED}},
        { "efr keypress practice description", new string [] {"When the <b>right button</b> text becomes bolded - press the\nright button\n\n" +
                                                              "When the <b>left button</b> text becomes bolded - press the\nleft button",
                                                              GERMAN_TRANSLATION_NEEDED}},
        { "new efr instructions title", new string[] { "Externalized Free Recall (EFR) Instructions" } },
        { "new efr instructions main", new string[] { "In this section of the study, we would like you to vocalize words that come to your mind during the free recall sections (the long sections directly following deliveries, NOT the recalls with store cues).\n\nPlease continue to recall as many words as possible from the just-presented list. In addition, every time a specific, salient word comes to mind, say it aloud, even if you have already recalled it or if it was not presented on the most recent delivery day.\n\nOnly say other words if they come to mind as you are trying to recall items on the most recently presented list. This is not a free-association task.\n\nIf the word you have just said aloud was NOT presented on the most recent list, or if you have already recalled it in this recall period, press the B key after recalling that word, but before recalling the next word.",
                                                      GERMAN_TRANSLATION_NEEDED} },

        { "new efr keypress practice main", new string [] { "Let's practice pressing the reject key.", GERMAN_TRANSLATION_NEEDED } },
        { "new efr keypress practice description", new string [] { "Press the (B) key 20 times and wait about 3 seconds between each keypress.\n\nThe screen will automatically continue when you are done.",
                                                              GERMAN_TRANSLATION_NEEDED } },

        { "new efr check understanding title", new string[] { "EFR Review", GERMAN_TRANSLATION_NEEDED } },
        { "new efr check understanding main", new string[] { "Please press the buzzer to call the researcher in now.", GERMAN_TRANSLATION_NEEDED } },

        { "navigation note title", new string[] { "Quick Note!", GERMAN_TRANSLATION_NEEDED } },
        { "navigation note main", new string[] { "Please navigate from store to store as quickly and efficiently as you can.", GERMAN_TRANSLATION_NEEDED } },

        { "classifier delay note title", new string[] { "Quick Note!", GERMAN_TRANSLATION_NEEDED } },
        { "classifier delay note main", new string[] { "For the remaining sessions, you may notice a slight lag once you arrive at each store. Please note that this is purposeful and not an error in the experiment.", GERMAN_TRANSLATION_NEEDED } },

        { "fixation item", new string [] {"+", "+"} },
        { "fixation practice message", new string [] {"Please look at the plus sign", GERMAN_TRANSLATION_NEEDED} },

        { "fr item", new string [] { "*******", "*******" } },
        { "speak now", new string [] { "(Please speak now)", GERMAN_TRANSLATION_NEEDED} },
        { "new efr message", new string [] { "Press the (B) key to reject a recalled item", GERMAN_TRANSLATION_NEEDED } },

        { "free recall title", new string [] { "Free Recall", GERMAN_TRANSLATION_NEEDED} },
        { "free recall main", new string [] { "Try to recall all the items that you delivered to the stores in this delivery day.\n\nType one item and press Enter to submit your response and type your next response", 
                                              GERMAN_TRANSLATION_NEEDED}}, 

        { "cued recall message", new string [] {"Press the (X) key after recalling the item to move to the next store", GERMAN_TRANSLATION_NEEDED}},
        { "cued recall title", new string [] { "Cued Recall", GERMAN_TRANSLATION_NEEDED} },
        { "online cued recall main", new string [] {"Please recall which item you delivered to the store shown on the screen.\n\nPress the Enter key after recalling the item to move to the next store", GERMAN_TRANSLATION_NEEDED}},

        { "final store recall title", new string [] {"Final Store Recall", GERMAN_TRANSLATION_NEEDED} },
        { "final store recall main", new string [] {"Try to recall stores that you delivered to.\n\nNote that you need to recall the store names", GERMAN_TRANSLATION_NEEDED} },
        { "final store recall text", new string [] {"Start typing store name one at a time...", GERMAN_TRANSLATION_NEEDED}},
        
        { "final object recall title", new string [] {"Final Object Recall", GERMAN_TRANSLATION_NEEDED} },
        { "final object recall main", new string [] {"Try to recall all the items that you delivered so far.", GERMAN_TRANSLATION_NEEDED} },
        { "final object recall text", new string [] {"Start typing item one at a time...", GERMAN_TRANSLATION_NEEDED}},

        { "play movie", new string[] {"Press any key to play movie.", "Starten Sie das Video mit beliebiger Taste."} },
        { "recording confirmation", new string[] {"Did you hear the recording? \n(Y = Continue / N = Try Again / C = Cancel).",
                                                  "War die Aufnahme verständlich? \n(Y = Ja, weiter / N = Neuer Versuch / C = Abbrechen)."} },
        { "playing", new string[] {"Playing...", "Spiele ab…"} },
        { "recording", new string[] {"Recording...", "Nehme auf…"} },
        { "after the beep", new string[] {"Press any key to record a sound after the beep.", "Drücken Sie eine beliebige Taste, um eine Testaufnahme zu starten."} },
        { "running participant", new string[] {"Running a new session of Delivery Person. \n Press (Y) to continue, (N) to quit.",
                                               "Wir starten jetzt eine neue Session Fahrradkurier.\n Drücken Sie (Y) um fortzufahren, (N) um abzubrechen.",} },
        { "begin session", new string[] {"Begin session", "Beginne Session"} },
        { "break", new string[] {"It's time for a short break.\nPlease wait for the researcher\nto come check on you before continuing the experiment.\n\nResearcher: Press space to resume the experiment.",
                                 GERMAN_TRANSLATION_NEEDED} },

        { "music video instructions title", new string[] { "Music Video Instructions", GERMAN_TRANSLATION_NEEDED } },
        { "music video instructions main", new string[] { "You will now be asked to watch a series of music videos. After each individual video, you will rate how familiar you are with the just presented video and how engaged you were during the presentation of the video. Use the Left Trigger and Right Trigger (big buttons on the back of the controller) to move the rating sliders. \n\nWe encourage you to engage with these videos as you would if you were watching them for leisure. \n\nOver the course of the study, the videos will repeat. Please do your best to avoid listening to the presented songs or watching the videos outside of the study. \n\nPlease let the experimenter know if you have any questions; then continue to the first video.",
                                                          GERMAN_TRANSLATION_NEEDED } },
        { "music video familiarity title", new string[] { "On a scale of very unfamiliar to very familiar, please rate your familiarity with this video.", GERMAN_TRANSLATION_NEEDED } },
        { "music video familiarity rating 0", new string[] { "very unfamiliar", GERMAN_TRANSLATION_NEEDED } },
        { "music video familiarity rating 1", new string[] { "somewhat unfamiliar", GERMAN_TRANSLATION_NEEDED } },
        { "music video familiarity rating 2", new string[] { "neutral", GERMAN_TRANSLATION_NEEDED } },
        { "music video familiarity rating 3", new string[] { "somewhat familiar", GERMAN_TRANSLATION_NEEDED } },
        { "music video familiarity rating 4", new string[] { "very familiar", GERMAN_TRANSLATION_NEEDED } },
        { "music video engagement title", new string[] { "On a scale of very disengaged to very engaged, please rate how engaged you were during the presentation of this video.", GERMAN_TRANSLATION_NEEDED } },
        { "music video engagement rating 0", new string[] { "very disengaged", GERMAN_TRANSLATION_NEEDED } },
        { "music video engagement rating 1", new string[] { "somewhat disengaged", GERMAN_TRANSLATION_NEEDED } },
        { "music video engagement rating 2", new string[] { "neutral", GERMAN_TRANSLATION_NEEDED } },
        { "music video engagement rating 3", new string[] { "somewhat engaged", GERMAN_TRANSLATION_NEEDED } },
        { "music video engagement rating 4", new string[] { "very engaged", GERMAN_TRANSLATION_NEEDED } },
        { "music video recall instructions title", new string[] { "Music Video Recall Instructions", GERMAN_TRANSLATION_NEEDED } },
        { "music video recall instructions main", new string[] { "Over the next ~10 minutes, you will be prompted to recall as much as you can from each music video that you have been shown during this session (3 minutes per video). \n\nOne at a time, the title of the song and a screencap from the video will appear on screen for 5 seconds. \n\nOnce these disappear from the screen, you can begin recalling as much as you remember from the music video. During this period, only recall information from the song/music video that was prompted. Be as descriptive as possible. \n\nPlease be aware that there is no right or wrong way to do this, and we encourage you to say whatever comes to mind when thinking back to these videos. \n\nPlease let the experimenter know if you have any questions; then continue to the first video recall.",
                                                    GERMAN_TRANSLATION_NEEDED } },
        { "music video ending instructions", new string[] {"\nPress (Y) to continue to the questions.", GERMAN_TRANSLATION_NEEDED } },

        { "please find prompt", new string[] {"please find the ", "Bitte finden Sie "} },
        { "bakery", new string[] {"bakery", "die Bäckerei"} },
        { "barber shop", new string[] {"barber shop", "den Friseur"} },
        { "bike shop", new string[] {"bike shop", "den Fahrradladen"} },
        { "cafe", new string[] {"cafe", "das Cafe"} },
        { "clothing store", new string[] {"clothing store", "das Kleidungsgeschäft"} },
        { "dentist", new string[] {"dentist", "den Zahnarzt"} },
        { "craft shop", new string[] {"craft shop", "den Bastelladen"} },
        { "grocery store", new string[] {"grocery store", "den Supermarkt"} },
        { "jewelry store", new string[] {"jewelry store", "den Juwelier"} },
        { "florist", new string[] {"florist", "den Blumenladen"} },
        { "hardware store", new string[] {"hardware store", "den Baumarkt"} },
        { "gym", new string[] {"gym", "das Fitnessstudio"} },
        { "pizzeria", new string[] {"pizzeria", "die Pizzeria"} },
        { "pet store", new string[] {"pet store", "die Tierhandlung"} },
        { "music store", new string[] {"music store", "das Musikgeschäft"} },
        { "pharmacy", new string[] {"pharmacy", "die Apotheke"} },
        { "toy store", new string[] {"toy store", "den Spielwarenladen"} }, 

        { "confetti", new string[] {"confetti", "Konfetti"} },
    };

    public static string GetLanguageString(string string_name)
    {
        if (!language_string_dict.ContainsKey(string_name))
            throw new UnityException("I don't have a language string called: " + string_name);
        return language_string_dict[string_name][(int)current_language];
    }
}
