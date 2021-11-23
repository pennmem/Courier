using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Luminosity.IO;

public class MessageImageDisplayer : MonoBehaviour
{
    public GameObject[] practice_phase_messages;
    public GameObject[] final_recall_messages;
    public GameObject[] delivery_restart_messages;
    public GameObject[] store_images_presentation_messages;

    public GameObject[] nicls_final_recall_messages; // TODO: JPB: Add the german slide
    public GameObject[] recap_instruction_messages_efr_2btn_en; // TODO: JPB: Make this work for german
    public GameObject[] recap_instruction_messages_efr_en;
    public GameObject[] recap_instruction_messages_fr_en;

    public GameObject[] music_video_prompts;

    public GameObject please_find_the_blah;
    public Text please_find_the_blah_text;
    public GameObject please_find_the_blah_reminder;
    public Text please_find_the_blah_reminder_text;
    public GameObject deliver_item_visual_dislay;
    public Text deliver_item_display_text;
    public GameObject free_recall_display;
    public GameObject efr_display;
    public GameObject cued_recall_message;
    public GameObject sliding_scale_display;
    public GameObject sliding_scale_2_display;
    public GameObject general_message_display;
    public GameObject general_big_message_display;
    public GameObject general_bigger_message_display;
    public ScriptedEventReporter scriptedEventReporter;
    public GameObject fpsDisplay;
    public Text fpsDisplayText;

    private const float BUTTON_MSG_DISPLAY_WAIT = 0.3f;
    private const int REQUIRED_VALID_BUTTON_PRESSES = 1;

    public enum ActionButton
    {
        LeftButton,
        RightButton,
        RejectButton,
        ContinueButton
    }

    public IEnumerator DisplayLanguageMessage(GameObject[] langMessages, string buttonName = "Continue")
    {
        yield return DisplayMessage(langMessages[(int)LanguageSource.current_language], buttonName);
    }

    public IEnumerator DisplayLanguageMessageTimed(GameObject[] langMessages, float time)
    {
        yield return DisplayMessageTimed(langMessages[(int)LanguageSource.current_language], time); 
    }

    public IEnumerator DisplayMessage(GameObject message, string buttonName = "Continue")
    {
        Dictionary<string, object> messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
        // TODO: JPB: (Hokua) Change this so that it takes a logging name (the message titleText or all text)
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

        message.SetActive(true);
        yield return null;
        if (buttonName == "")
            while (!InputManager.anyKeyDown)
                yield return null;
        else
            while (!InputManager.GetButtonDown(buttonName) && !InputManager.GetButtonDown("Secret"))
                yield return null;
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
        message.SetActive(false);
    }

    public IEnumerator DisplayMessageFunction(GameObject message, Func<IEnumerator> func)
    {
        Dictionary<string, object> messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
        // TODO: JPB: (Hokua) Change this so that it takes a logging name (the message titleText or all text)
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

        message.SetActive(true);
        yield return func();

        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
        message.SetActive(false);
    }

    public IEnumerator DisplayMessageTimed(GameObject message, float waitTime)
    {
        Dictionary<string, object> messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
        // TODO: JPB: (Hokua) Change this so that it takes a logging name (the message titleText or all text)
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

        message.SetActive(true);
        float startTime = Time.time;
        while (Time.time < startTime + waitTime)
        {
            yield return null;

            if (InputManager.GetButtonDown("Secret"))
                break;
            else if (InputManager.GetButtonDown("Continue"))
                scriptedEventReporter.ReportScriptedEvent("keypress",
                    new Dictionary<string, object> { { "response", "incorrect" } });
            else if (InputManager.anyKeyDown)
            {
                foreach (KeyCode kcode in System.Enum.GetValues(typeof(KeyCode)))
                {
                    if (InputManager.GetKey(kcode))
                        scriptedEventReporter.ReportScriptedEvent("keypress",
                            new Dictionary<string, object> { { "response", kcode.ToString() } });
                }
            }
        }
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
        message.SetActive(false);
    }

    public IEnumerator DisplayMessageLRKeypressBold(GameObject message, ActionButton boldButton,
        string leftLogMessage = "leftKey", string rightLogMessage = "rightKey")
    {
        // Report instruction displayed
        var messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
        // TODO: JPB: (Hokua) Change this so that it takes a logging name (the message titleText or all text)
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

        message.SetActive(true);
        while (true)
        {
            yield return null;

            if (InputManager.GetButtonDown("Secret"))
            {
                scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", "Secret" } });
                break;
            }
            else if (InputManager.GetButtonDown("EfrLeft") && (boldButton == ActionButton.LeftButton))
            {
                scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", leftLogMessage } });
                Text toggleText = message.transform.Find("left button text").GetComponent<Text>();
                yield return DoTextBoldTimedOrButton("EfrLeft", toggleText, BUTTON_MSG_DISPLAY_WAIT);
                break;
            }
            else if (InputManager.GetButtonDown("EfrRight") && (boldButton == ActionButton.RightButton))
            {
                scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", rightLogMessage } });
                Text toggleText = message.transform.Find("right button text").GetComponent<Text>();
                yield return DoTextBoldTimedOrButton("EfrRight", toggleText, BUTTON_MSG_DISPLAY_WAIT);
                break;
            }
            else if (InputManager.anyKeyDown)
            {
                foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
                {
                    if (InputManager.GetKey(kcode))
                        scriptedEventReporter.ReportScriptedEvent("keypress",
                            new Dictionary<string, object> { { "response", kcode.ToString() } });
                }
            }
        }

        // Report instruction cleared
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
        message.SetActive(false);
    }

    // TODO: JPB: (Hokeu) Add practice looping like DisplayMessageTimedLRKeypressBold has
    public IEnumerator DisplayMessageTimedKeypressBold(GameObject message, float waitTime, ActionButton boldButton, string boldTextName,
        string buttonLogMessage = "button", bool breakOnKeypress = false, float minWaitTime = 0f)
    {
        // Report instruction displayed
        var messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
        // TODO: JPB: (Hokua) Change this so that it takes a logging name (the message titleText or all text)
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

        message.SetActive(true);
        float startTime = Time.time;
        while (Time.time < startTime + waitTime)
        {
            yield return null;

            if (InputManager.GetButtonDown("Secret"))
            {
                scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", "Secret" } });
                break;
            }
            else if (InputManager.GetButtonDown("EfrLeft") && (boldButton == ActionButton.LeftButton))
            {
                scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", buttonLogMessage } });
                Text toggleText = message.transform.Find(boldTextName).GetComponent<Text>();
                yield return DoTextBoldTimedOrButton("EfrLeft", toggleText, BUTTON_MSG_DISPLAY_WAIT);
                if (breakOnKeypress && (Time.time >= startTime + minWaitTime))
                    break;
            }
            else if (InputManager.GetButtonDown("EfrRight") && (boldButton == ActionButton.RightButton))
            {
                scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", buttonLogMessage } });
                Text toggleText = message.transform.Find(boldTextName).GetComponent<Text>();
                yield return DoTextBoldTimedOrButton("EfrRight", toggleText, BUTTON_MSG_DISPLAY_WAIT);
                if (breakOnKeypress && (Time.time >= startTime + minWaitTime))
                    break;
            }
            else if (InputManager.GetButtonDown("EfrReject") && (boldButton == ActionButton.RejectButton))
            {
                scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", buttonLogMessage } });
                Text toggleText = message.transform.Find(boldTextName).GetComponent<Text>();
                yield return DoTextBoldTimedOrButton("EfrReject", toggleText, BUTTON_MSG_DISPLAY_WAIT);
                if (breakOnKeypress && (Time.time >= startTime + minWaitTime))
                    break;
            }
            else if (InputManager.GetButtonDown("Continue") && (boldButton == ActionButton.ContinueButton))
            {
                scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", buttonLogMessage } });
                Text toggleText = message.transform.Find(boldTextName).GetComponent<Text>();
                yield return DoTextBoldTimedOrButton("Continue", toggleText, BUTTON_MSG_DISPLAY_WAIT);
                if (breakOnKeypress && (Time.time >= startTime + minWaitTime))
                    break;
            }
            else if (InputManager.anyKeyDown)
            {
                foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
                {
                    if (InputManager.GetKey(kcode))
                        scriptedEventReporter.ReportScriptedEvent("keypress",
                            new Dictionary<string, object> { { "response", kcode.ToString() } });
                }
            }
        }

        // Report instruction cleared
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);

        message.SetActive(false);
    }

    public IEnumerator DisplayMessageTimedLRKeypressBold(GameObject display, float waitTime, 
        string leftLogMessage = "leftKey", string rightLogMessage = "rightKey", bool retry = false, bool breakOnKeypress = false)
    {
        Text leftText = display.transform.Find("left button text").GetComponent<Text>();
        Text rightText = display.transform.Find("right button text").GetComponent<Text>();

        int numValidButtonPresses = 0;
        while (numValidButtonPresses < REQUIRED_VALID_BUTTON_PRESSES)
        {
            // Report instruction displayed
            var messageData = new Dictionary<string, object>();
            messageData.Add("message name", display.name);
            // TODO: JPB: (Hokua) Change this so that it takes a logging name (the message titleText or all text)
            scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

            display.SetActive(true);

            float startTime = Time.time;
            while (Time.time < startTime + waitTime)
            {
                yield return null;

                if (InputManager.GetButtonDown("Secret"))
                {
                    scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", "Secret" } });
                    break;
                }
                else if (InputManager.GetButtonDown("EfrLeft"))
                {
                    scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", leftLogMessage } });
                    yield return DoTextBoldTimedOrButton("EfrLeft", leftText, BUTTON_MSG_DISPLAY_WAIT);
                    numValidButtonPresses++;
                    if (breakOnKeypress)
                        break;
                }
                else if (InputManager.GetButtonDown("EfrRight"))
                {
                    scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", rightLogMessage } });
                    yield return DoTextBoldTimedOrButton("EfrRight", rightText, BUTTON_MSG_DISPLAY_WAIT);
                    numValidButtonPresses++;
                    if (breakOnKeypress)
                        break;
                }
                else if (InputManager.anyKeyDown)
                {
                    foreach (KeyCode kcode in Enum.GetValues(typeof(KeyCode)))
                    {
                        if (InputManager.GetKey(kcode))
                            scriptedEventReporter.ReportScriptedEvent("keypress",
                                new Dictionary<string, object> { { "response", kcode.ToString() } });
                    }
                }
            }

            // Report instruction cleared
            scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);

            display.SetActive(false);

            if (!retry)
            {
                break;
            }
            else if (numValidButtonPresses < REQUIRED_VALID_BUTTON_PRESSES)
            {
                SetGeneralMessageText(mainText: "efr check try again main",
                                      descriptiveText: "efr check try again description");
                yield return DisplayMessage(general_message_display);
            }
        }
    }

    public IEnumerator DisplaySlidingScaleMessage(GameObject message, string buttonName = "Continue")
    {
        Dictionary<string, object> messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
        // TODO: JPB: (Hokua) Change this so that it takes a logging name (the message titleText or all text)
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

        message.SetActive(true);
        yield return null;
        while (!InputManager.GetButtonDown(buttonName) && !InputManager.GetButtonDown("Secret"))
        {
            yield return null;
            if (InputManager.GetButtonDown("EfrLeft"))
                message.transform.Find("sliding scale").GetComponent<Slider>().value -= 1;
            else if (InputManager.GetButtonDown("EfrRight"))
                message.transform.Find("sliding scale").GetComponent<Slider>().value += 1;
        }
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
        message.SetActive(false);

        scriptedEventReporter.ReportScriptedEvent("sliding scale value",
            new Dictionary<string, object>() { { "value", message.transform.Find("sliding scale").GetComponent<Slider>().value } });
    }

    // TODO: JPB: (Hokue) Combine with the above function (or change name)
    public IEnumerator DisplaySlidingScale2Message(GameObject message, string buttonName = "Continue")
    {
        Dictionary<string, object> messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
        // TODO: JPB: (Hokua) Change this so that it takes a logging name (the message titleText or all text)
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

        message.SetActive(true);
        yield return null;
        var slider = message.transform.Find("sliding scale").GetComponent<Slider>();
        // TODO: JPB: (Hokue) Change this so that function takes a list of illegal values (or a bool to make the middle illegal)
        while ( (!InputManager.GetButtonDown(buttonName) && !InputManager.GetButtonDown("Secret"))
                || slider.value ==  1)
        {
            yield return null;
            if (InputManager.GetButtonDown("EfrLeft"))
                message.transform.Find("sliding scale").GetComponent<Slider>().value -= 1;
            else if (InputManager.GetButtonDown("EfrRight"))
                message.transform.Find("sliding scale").GetComponent<Slider>().value += 1;
        }
        Debug.Log(slider.value);
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
        message.SetActive(false);

        scriptedEventReporter.ReportScriptedEvent("sliding scale value",
            new Dictionary<string, object>() { { "value", message.transform.Find("sliding scale").GetComponent<Slider>().value } });
    }

    // Display message for cued recall
    public void SetCuedRecallMessage(string bottomText)
    {
        cued_recall_message.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(bottomText);
    }

    public void SetReminderText(string store_name)
    {
        string prompt_string = LanguageSource.GetLanguageString("please find prompt") + "<b>" + LanguageSource.GetLanguageString(store_name) + "</b>";
        please_find_the_blah_reminder_text.text = prompt_string;
    }

    public void SetDeliverItemText(string name)
    {
        string prompt_string = name;
        string update_name = "";
        foreach (char c in prompt_string)
        {
            if(char.IsLetter(c)||c == '\'')
                update_name += char.ToLower(c);
            else
                update_name += " ";
            
        }
        Button btn = deliver_item_visual_dislay.GetComponent<Button>();
        deliver_item_display_text.text = update_name;
    }

    public void SetEfrText(string titleText = "", string descriptiveText = "", string leftButton = null, string rightButton = null)
    {
        if (titleText != null)
            efr_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetLanguageString(titleText);
        if (descriptiveText != null)
            efr_display.transform.Find("descriptive text").GetComponent<Text>().text = LanguageSource.GetLanguageString(descriptiveText);
        if (leftButton != null)
            efr_display.transform.Find("left button text").GetComponent<Text>().text = LanguageSource.GetLanguageString(leftButton);
        if (rightButton != null)
            efr_display.transform.Find("right button text").GetComponent<Text>().text = LanguageSource.GetLanguageString(rightButton);
    }

    public void SetEfrElementsActive(bool speakNowText = false, bool descriptiveText = false, 
                                     bool controllerLeftButtonImage = false, bool controllerRightButtonImage = false)
    {
        efr_display.transform.Find("speak now text").GetComponent<Text>().gameObject.SetActive(speakNowText);
        efr_display.transform.Find("descriptive text").GetComponent<Text>().gameObject.SetActive(descriptiveText);
        efr_display.transform.Find("controller left button image")
                   .GetComponent<Image>().gameObject.SetActive(controllerLeftButtonImage);
        efr_display.transform.Find("controller right button image")
                   .GetComponent<Image>().gameObject.SetActive(controllerRightButtonImage);
    }

    public void SetEfrTextResize(float LeftButtonSize = 0, float rightButtonSize = 0)
    {
        // Left Button
        Text leftText = efr_display.transform.Find("left button text").GetComponent<Text>();
        leftText.GetComponent<RectTransform>().anchorMin -= new Vector2(0, LeftButtonSize / 100);
        leftText.GetComponent<RectTransform>().anchorMax += new Vector2(0, LeftButtonSize / 100);

        // Right Button
        Text rightText = efr_display.transform.Find("right button text").GetComponent<Text>();
        rightText.GetComponent<RectTransform>().anchorMin -= new Vector2(0f, rightButtonSize / 100);
        rightText.GetComponent<RectTransform>().anchorMax += new Vector2(0f, rightButtonSize / 100);
    }

    // TODO: JPB: (Hokua) See if this can be combined with FpsDisplayer.cs in some way
    // TODO: JPB: Change all "continue text" to "bottom text"
    public void SetFPSDisplayText(string fpsValue = "", string mainText = "", string continueText = "continue")
    {
        if (fpsValue != null)
            general_big_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetLanguageString("frame test end title") + fpsValue;
        if (mainText != null)
            general_big_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetLanguageString(mainText);
        if (continueText != null)
            general_big_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(continueText);
    }

    public void SetGeneralMessageText(string titleText = "", string mainText = "", string descriptiveText = "", string continueText = "continue",
                                      string[] ttFormatVals = null, string[] mtFormatVals = null, string[] dtFormatVals = null, string[] ctFormatVals = null)
    {
        if (titleText != null)
            if (ttFormatVals == null)
                general_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetLanguageString(titleText);
            else
                general_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetFormattableLanguageString(titleText, ttFormatVals);
        if (mainText != null)
            if (mtFormatVals == null)
                general_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetLanguageString(mainText);
            else
                general_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetFormattableLanguageString(mainText, mtFormatVals);
        if (descriptiveText != null)
            if (dtFormatVals == null)
                general_message_display.transform.Find("descriptive text").GetComponent<Text>().text = LanguageSource.GetLanguageString(descriptiveText);
            else
                general_message_display.transform.Find("descriptive text").GetComponent<Text>().text = LanguageSource.GetFormattableLanguageString(descriptiveText, dtFormatVals);
        if (continueText != null)
            if (ctFormatVals == null)
                general_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(continueText);
            else
                general_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetFormattableLanguageString(continueText, ctFormatVals);
    }

    public void SetGeneralBigMessageText(string titleText = "", string mainText = "", string continueText = "continue",
                                         string[] ttFormatVals = null, string[] mtFormatVals = null, string[] ctFormatVals = null)
    {
        if (titleText != null)
            if (ttFormatVals == null)
                general_big_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetLanguageString(titleText);
            else
                general_big_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetFormattableLanguageString(titleText, ttFormatVals);
        if (mainText != null)
            if (mtFormatVals == null)
                general_big_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetLanguageString(mainText);
            else
                general_big_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetFormattableLanguageString(mainText, mtFormatVals);
        if (continueText != null)
            if (ctFormatVals == null)
                general_big_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(continueText);
            else
                general_big_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetFormattableLanguageString(continueText, ctFormatVals);
    }

    public void SetGeneralBiggerMessageText(string titleText = "", string mainText = "", string continueText = "continue",
                                            string[] ttFormatVals = null, string[] mtFormatVals = null, string[] ctFormatVals = null)
    {
        if (titleText != null)
            if (ttFormatVals == null)
                general_bigger_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetLanguageString(titleText);
            else
                general_bigger_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetFormattableLanguageString(titleText, ttFormatVals);
        if (mainText != null)
            if (mtFormatVals == null)
                general_bigger_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetLanguageString(mainText);
            else
                general_bigger_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetFormattableLanguageString(mainText, mtFormatVals);
        if (continueText != null)
            if (ctFormatVals == null)
                general_bigger_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(continueText);
            else
                general_bigger_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetFormattableLanguageString(continueText, ctFormatVals);
    }

    public void SetSlidingScaleText(string mainText = "", string[] ratings = null, string continueText = "continue")
    {
        sliding_scale_display.transform.Find("sliding scale").GetComponent<Slider>().value = 2;

        if (mainText != null)
            sliding_scale_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetLanguageString(mainText);

        int numRatings = sliding_scale_display.transform.Find("ratings").childCount;
        if (ratings != null)
            for (int i = 0; i < numRatings; ++i)
                sliding_scale_display.transform.Find("ratings/rating " + i).GetComponent<Text>().text = LanguageSource.GetLanguageString(ratings[i]);
        else // ratings == null
            for (int i = 0; i < numRatings; ++i)
                sliding_scale_display.transform.Find("ratings/rating " + i).GetComponent<Text>().text = LanguageSource.GetLanguageString("");

        if (continueText != null)
            sliding_scale_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(continueText);
    }

    public void SetSlidingScale2Text(string mainText = "", string[] ratings = null, string continueText = "continue")
    {
        sliding_scale_2_display.transform.Find("sliding scale").GetComponent<Slider>().value = 1;

        if (mainText != null)
            sliding_scale_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetLanguageString(mainText);

        int numRatings = sliding_scale_2_display.transform.Find("ratings").childCount;
        if (ratings != null)
            for (int i = 0; i < numRatings; ++i)
                sliding_scale_2_display.transform.Find("ratings/rating " + i).GetComponent<Text>().text = LanguageSource.GetLanguageString(ratings[i]);
        else // ratings == null
            for (int i = 0; i < numRatings; ++i)
                sliding_scale_2_display.transform.Find("ratings/rating " + i).GetComponent<Text>().text = LanguageSource.GetLanguageString("");

        if (continueText != null)
            sliding_scale_2_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(continueText);
    }

    private IEnumerator DoTextBoldTimedOrButton(string buttonName, Text displayText, float waitTime)
    {
        string buttonText = displayText.text;
        Vector2 anchorMin = displayText.GetComponentInParent<RectTransform>().anchorMin;
        Vector2 anchorMax = displayText.GetComponentInParent<RectTransform>().anchorMax;

        // Bold and increase font
        displayText.text = "<b>" + buttonText + "</b>";
        displayText.GetComponentInParent<RectTransform>().anchorMin -= new Vector2(0, 0.003f);
        displayText.GetComponentInParent<RectTransform>().anchorMax += new Vector2(0, 0.003f);

        // Wait for timeout and button release
        float startTime = Time.time;
        while ((Time.time < startTime + waitTime) || InputManager.GetButton(buttonName))
            yield return null;

        // Unbold and decrease font
        displayText.GetComponentInParent<RectTransform>().anchorMin = anchorMin;
        displayText.GetComponentInParent<RectTransform>().anchorMax = anchorMax;
        displayText.text = buttonText;
    }
}
