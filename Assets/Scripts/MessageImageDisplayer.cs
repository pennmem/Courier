using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

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
    public UnityEngine.UI.Text please_find_the_blah_text;
    public GameObject please_find_the_blah_reminder;
    public UnityEngine.UI.Text please_find_the_blah_reminder_text;
    public GameObject deliver_item_visual_dislay;
    public UnityEngine.UI.Text deliver_item_display_text;
    public UnityEngine.UI.Text please_speak_now_text;
    public ScriptedEventReporter scriptedEventReporter;

    private const float BUTTON_MSG_DISPLAY_WAIT = 0.3f;

    public IEnumerator DisplayLanguageMessage(GameObject[] language_messages)
    {
        yield return DisplayMessage(language_messages[(int)LanguageSource.current_language]);
    }

    //DisplayLanguageMessageFixedDuration shows the game object for a fixed amount of time, X keypress not required to proceed
    public IEnumerator DisplayLanguageMessageFixedDuration(GameObject[] m, float time)
    {
        yield return DisplayMessageWithoutX(m[(int)LanguageSource.current_language], time); 
    }
    //DisplayLanguageMessageFixedDurationKeyPress shows the game object for a fixed amount of time, X keypress not required to proceed
    //it also records the keypress during the display
    public IEnumerator DisplayLanguageMessageFixedDurationKeyPress(GameObject[] m, GameObject[] m_left, GameObject[] m_right, float time)
    {
        yield return DisplayMessageWithoutX_Keypress(m[(int)LanguageSource.current_language], m_left[(int)LanguageSource.current_language], m_right[(int)LanguageSource.current_language], time);
    }

    //display message for cued recall
    public void SetCuedRecallMessage(bool isActive)
    {
        GameObject message = cued_recall_message[(int)LanguageSource.current_language];
        message.SetActive(isActive);
    }

    private IEnumerator DisplayMessage(GameObject message)
    {
        Dictionary<string, object> messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);
        message.SetActive(true);
        yield return null;
        while (!Input.GetButtonDown("x (continue)"))
            yield return null;
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
        message.SetActive(false);
    }

    private IEnumerator DisplayMessageWithoutX(GameObject message, float waitTime)
    {
        Dictionary<string, object> messageData = new Dictionary<string, object>();
        messageData.Add("message name", message.name);
        scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);
        message.SetActive(true);
        yield return null;
        float startTime = Time.time;
        while (Time.time < startTime + waitTime)
        {
            if (Input.GetButtonDown("q (secret)"))
                break;
            yield return null;
        }
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
        message.SetActive(false);
    }

    private IEnumerator DisplayMessageWithoutX_Keypress(GameObject message, GameObject message_left, GameObject message_right, float waitTime)
    {
        const int REQUIRED_VALID_BUTTON_PRESSES = 1;
        int numValidButtonPresses = 0;
        while (numValidButtonPresses < REQUIRED_VALID_BUTTON_PRESSES)
        {
            Dictionary<string, object> messageData = new Dictionary<string, object>();
            messageData.Add("message name", message.name);
            scriptedEventReporter.ReportScriptedEvent("instruction message displayed", messageData);
            message.SetActive(true);
            yield return null;
            float startTime = Time.time;
            Dictionary<string, object> data = new Dictionary<string, object>();
            data.Add("recording start", startTime);
            int i = 0;

            while (Time.time < startTime + waitTime)
            {
                float currTime = Time.time / 100f;  // centi-seconds to seconds 

                if (Input.GetButtonDown("correct"))
                {
                    string keypressInfo = i.ToString() + "th keypress: correct";
                    data.Add(keypressInfo, currTime);
                    numValidButtonPresses++;
                    i++;

                    message_right.SetActive(true);
                    message.SetActive(false);
                    while (Time.time < currTime + BUTTON_MSG_DISPLAY_WAIT || Input.GetButton("correct"))
                    {
                        yield return null;
                    }
                    message_right.SetActive(false);
                    message.SetActive(true);
                }
                else if (Input.GetButtonDown("false"))
                {
                    string keypressInfo = i.ToString() + "th keypress: incorrect";
                    data.Add(keypressInfo, currTime);
                    numValidButtonPresses++;
                    i++;

                    message.SetActive(false);
                    message_left.SetActive(true);
                    while (Time.time < currTime + BUTTON_MSG_DISPLAY_WAIT || Input.GetButton("false"))
                    {
                        yield return null;
                    }
                    message_left.SetActive(false);
                    message.SetActive(true);
                }
                else if (Input.anyKeyDown)
                {
                    foreach (KeyCode kcode in System.Enum.GetValues(typeof(KeyCode)))
                    {
                        if (Input.GetKey(kcode))
                        {
                            string keypressInfo = i.ToString() + "th keypress: " + kcode.ToString();
                            data.Add(keypressInfo, currTime);
                            i++;
                        }
                    }
                    
                }

                yield return null;
            }
            scriptedEventReporter.ReportScriptedEvent("key press", data);
            scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
            message.SetActive(false);

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

    public void SetSpeakNowText(string text)
    {
        please_speak_now_text.text = text;
    }
}
