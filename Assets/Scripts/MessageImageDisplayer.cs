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

    public GameObject[] nicls_final_recall_messages; // JPB: TODO: Add the german slide
    public GameObject[] recap_instruction_messages_efr_2btn_en; // JPB: TODO: Make this work for german
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
    public GameObject general_message_display;
    public GameObject general_big_message_display;
    public GameObject general_bigger_message_display;
    public ScriptedEventReporter scriptedEventReporter;
    public GameObject fpsDisplay;
    public Text fpsDisplayText;

    private const float BUTTON_MSG_DISPLAY_WAIT = 0.3f;
    private const int REQUIRED_VALID_BUTTON_PRESSES = 1;

    public enum EfrButton
    {
        LeftButton,
        RightButton
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
        // JPB: TODO: Change this so that it takes a logging name
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

    public IEnumerator DisplayMessageTimed(GameObject message, float waitTime)
    {
        Dictionary<string, object> messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
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

    public IEnumerator DisplayMessageKeypressBold(GameObject display, EfrButton boldButton)
    {
        display.SetActive(true);

        // Report instruction displayed
        var messageData = new Dictionary<string, object>();
        messageData.Add("message name", display.name);
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

        while (true)
        {
            yield return null;

            if (InputManager.GetButtonDown("Secret"))
            {
                break;
            }
            else if (InputManager.GetButtonDown("EfrLeft") && (boldButton == EfrButton.LeftButton))
            {
                Text toggleText = display.transform.Find("left button text").GetComponent<Text>();
                yield return DoTextBoldTimedOrButton("EfrLeft", toggleText, BUTTON_MSG_DISPLAY_WAIT);
                break;
            }
            else if (InputManager.GetButtonDown("EfrRight") && (boldButton == EfrButton.RightButton))
            {
                Text toggleText = display.transform.Find("right button text").GetComponent<Text>();
                yield return DoTextBoldTimedOrButton("EfrRight", toggleText, BUTTON_MSG_DISPLAY_WAIT);
                break;
            }
        }

        // Report instruction cleared
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);

        display.SetActive(false);
    }

    public IEnumerator DisplayMessageTimedLRKeypressBold(GameObject display, float waitTime, 
        string leftLogMessage = "leftKey", string rightLogMessage = "rightKey", bool retry = false)
    {
        Text leftText = display.transform.Find("left button text").GetComponent<Text>();
        Text rightText = display.transform.Find("right button text").GetComponent<Text>();

        // Report instruction displayed
        var messageData = new Dictionary<string, object>();
        messageData.Add("message name", display.name);
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

        int numValidButtonPresses = 0;
        while (numValidButtonPresses < REQUIRED_VALID_BUTTON_PRESSES)
        {
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
                }
                else if (InputManager.GetButtonDown("EfrRight"))
                {
                    scriptedEventReporter.ReportScriptedEvent("keypress",
                        new Dictionary<string, object> { { "response", rightLogMessage } });
                    yield return DoTextBoldTimedOrButton("EfrRight", rightText, BUTTON_MSG_DISPLAY_WAIT);
                    numValidButtonPresses++;
                }
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
        // JPB: TODO: Change this so that it takes a logging name
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

    //display message for cued recall
    public void SetCuedRecallMessage(bool isActive)
    {
        cued_recall_message.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString("cued recall message");
        cued_recall_message.SetActive(isActive);
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

    public void SetFPSDisplayText(string fpsValue = "", string mainText = "", string continueText = "continue")
    {
        if (fpsValue != null)
            general_big_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetLanguageString("frame test end title") + fpsValue;
        if (mainText != null)
            general_big_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetLanguageString(mainText);
        if (continueText != null)
            general_big_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(continueText);
    }

    public void SetSlidingScaleText(string titleText = "", string[] ratings = null, string continueText = "continue")
    {
        sliding_scale_display.transform.Find("sliding scale").GetComponent<Slider>().value = 2;

        if (titleText != null)
            sliding_scale_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetLanguageString(titleText);

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

    public void SetGeneralMessageText(string titleText = "", string mainText = "", string descriptiveText = "", string continueText = "continue")
    {
        if (titleText != null)
            general_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetLanguageString(titleText);
        if (mainText != null)
            general_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetLanguageString(mainText);
        if (descriptiveText != null)
            general_message_display.transform.Find("descriptive text").GetComponent<Text>().text = LanguageSource.GetLanguageString(descriptiveText);
        if (continueText != null)
            general_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(continueText);
    }

    public void SetGeneralBigMessageText(string titleText = "", string mainText = "", string continueText = "continue")
    {
        if (titleText != null)
            general_big_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetLanguageString(titleText);
        if (mainText != null)
            general_big_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetLanguageString(mainText);
        if (continueText != null)
            general_big_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(continueText);
    }

    public void SetGeneralBiggerMessageText(string titleText = "", string mainText = "", string continueText = "continue")
    {
        if (titleText != null)
            general_bigger_message_display.transform.Find("title text").GetComponent<Text>().text = LanguageSource.GetLanguageString(titleText);
        if (mainText != null)
            general_bigger_message_display.transform.Find("main text").GetComponent<Text>().text = LanguageSource.GetLanguageString(mainText);
        if (continueText != null)
            general_bigger_message_display.transform.Find("continue text").GetComponent<Text>().text = LanguageSource.GetLanguageString(continueText);
    }

    public IEnumerator DoTextBoldTimedOrButton(string buttonName, Text displayText, float waitTime)
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
