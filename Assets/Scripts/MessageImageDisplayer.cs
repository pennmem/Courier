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
    public GameObject[] free_recall_message;
    public GameObject[] free_recall_keypress_message;
    public GameObject[] free_recall_keypress_message_bold_left;
    public GameObject[] free_recall_keypress_message_bold_right;
    public GameObject[] free_recall_retry_message;
    public GameObject[] fixation_message;
    public GameObject[] practice_fixation_message;
    public GameObject[] object_recall_message;
    public GameObject[] object_recall_message_bold_left;
    public GameObject[] object_recall_message_bold_right;
    public GameObject[] store_recall_message;
    public GameObject[] store_recall_message_bold_left;
    public GameObject[] store_recall_message_bold_right;
    public GameObject[] cued_recall_message;

    public GameObject please_find_the_blah;
    public Text please_find_the_blah_text;
    public GameObject please_find_the_blah_reminder;
    public Text please_find_the_blah_reminder_text;
    public GameObject deliver_item_visual_dislay;
    public Text deliver_item_display_text;
    public GameObject free_recall_display;
    public GameObject efr_display;
    public Text efr_display_title;
    public Text efr_display_left_button;
    public Text efr_display_right_button;
    public ScriptedEventReporter scriptedEventReporter;

    private const float BUTTON_MSG_DISPLAY_WAIT = 0.3f;
    private const int REQUIRED_VALID_BUTTON_PRESSES = 1;

    public IEnumerator DisplayLanguageMessage(GameObject[] language_messages, string buttonName = "Continue")
    {
        yield return DisplayMessage(language_messages[(int)LanguageSource.current_language], buttonName);
    }

    //DisplayLanguageMessageFixedDuration shows the game object for a fixed amount of time, X keypress not required to proceed
    public IEnumerator DisplayLanguageMessageTimed(GameObject[] m, float time)
    {
        yield return DisplayMessageTimed(m[(int)LanguageSource.current_language], time); 
    }
    //DisplayLanguageMessageFixedDurationKeyPress shows the game object for a fixed amount of time, X keypress not required to proceed
    //it also records the keypress during the display
    //public IEnumerator DisplayLanguageMessageTimedWithKeypressToggle(GameObject[] m, GameObject[] m_left, GameObject[] m_right, float time)
    //{
    //    yield return DisplayMessageTimedWithKeypressToggle(m[(int)LanguageSource.current_language], m_left[(int)LanguageSource.current_language], m_right[(int)LanguageSource.current_language], time);
    //}

    //display message for cued recall
    public void SetCuedRecallMessage(bool isActive)
    {
        GameObject message = cued_recall_message[(int)LanguageSource.current_language];
        message.SetActive(isActive);
    }

    public IEnumerator DisplayMessage(GameObject message, string buttonName = "Continue")
    {
        Dictionary<string, object> messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);
        message.SetActive(true);
        yield return null;
        if (buttonName == "")
            while (!InputManager.anyKeyDown)
                yield return null;
        else
            while (!InputManager.GetButtonDown(buttonName))
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
        yield return null;
        float startTime = Time.time;
        while (Time.time < startTime + waitTime)
        {
            if (InputManager.GetButtonDown("Secret"))
                break;
            yield return null;
        }
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
        message.SetActive(false);
    }

    public IEnumerator DisplayMessageTimedWithKeypressToggle(
        GameObject display, Text correctText, Text incorrectText, float waitTime, bool repeat = false)
    {
        var messageData = new Dictionary<string, object>();
        messageData.Add("message name", display.name);
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);

        int numValidButtonPresses = 0;
        while (numValidButtonPresses < REQUIRED_VALID_BUTTON_PRESSES)
        {
            display.SetActive(true);

            Dictionary<string, object> data = new Dictionary<string, object>();
            data.Add("recording start", DataReporter.RealWorldTime());
            var keypresses = new List<Dictionary<string, object>>();

            float startTime = Time.time;
            while (Time.time < startTime + waitTime)
            {
                yield return null;

                if (InputManager.GetButtonDown("Secret"))
                {
                    break;
                }
                else if (InputManager.GetButtonDown("Correct"))
                {
                    keypresses.Add(new Dictionary<string, object> { { "time", DataReporter.RealWorldTime() }, { "response", "correct" } });
                    numValidButtonPresses++;
                    yield return SetTextBoldTimed("Correct", correctText, BUTTON_MSG_DISPLAY_WAIT);
                }
                else if (InputManager.GetButtonDown("Incorrect"))
                {
                    keypresses.Add(new Dictionary<string, object> { { "time", DataReporter.RealWorldTime() }, { "response", "incorrect" } });
                    numValidButtonPresses++;
                    yield return SetTextBoldTimed("Incorrect", incorrectText, BUTTON_MSG_DISPLAY_WAIT);
                }
                else if (InputManager.anyKeyDown)
                {
                    foreach (KeyCode kcode in System.Enum.GetValues(typeof(KeyCode)))
                    {
                        if (InputManager.GetKey(kcode))
                        {
                            keypresses.Add(new Dictionary<string, object> { { "time", DataReporter.RealWorldTime() }, { "response", kcode.ToString() } });
                        }
                    }
                }
            }

            data.Add("keypresses", keypresses);
            scriptedEventReporter.ReportScriptedEvent("keypress data", data);
            scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);

            display.SetActive(false);

            if (!repeat)
                break;

            if (numValidButtonPresses < REQUIRED_VALID_BUTTON_PRESSES)
            {
                yield return DisplayLanguageMessage(free_recall_retry_message);
            }
        }
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
            {
                update_name += char.ToLower(c);
            } 
            else
            {
                update_name += " ";
            }
            
        }
        Button btn = deliver_item_visual_dislay.GetComponent<Button>();
        deliver_item_display_text.text = update_name;
    }

    public void SetEfrText(string title, string leftButton, string rightButton)
    {
        efr_display_title.text = title;
        efr_display_left_button.text = leftButton;
        efr_display_right_button.text = rightButton;
    }

    public IEnumerator SetTextBoldTimed(string buttonName, Text displayText, float waitTime)
    {
        string buttonText = displayText.text;
        Vector2 anchorMin = displayText.GetComponentInParent<RectTransform>().anchorMin;
        Vector2 anchorMax = displayText.GetComponentInParent<RectTransform>().anchorMax;

        displayText.text = "<b>" + buttonText + "</b>";
        displayText.GetComponentInParent<RectTransform>().anchorMin -= new Vector2(0.01f, 0f);
        displayText.GetComponentInParent<RectTransform>().anchorMax += new Vector2(0.01f, 0f);

        float startTime = Time.time;
        float currTime = startTime;
        while ((currTime < startTime + waitTime) || InputManager.GetButton(buttonName))
        {
            currTime = Time.time;
            yield return null;
        }

        displayText.GetComponentInParent<RectTransform>().anchorMin = anchorMin;
        displayText.GetComponentInParent<RectTransform>().anchorMax = anchorMax;
        displayText.text = buttonText;
    }
}
