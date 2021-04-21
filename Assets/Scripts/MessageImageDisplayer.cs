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
    public GameObject[] fixation_message;
    public GameObject[] object_recall_message;
    public GameObject[] store_recall_message;

    public GameObject please_find_the_blah;
    public UnityEngine.UI.Text please_find_the_blah_text;
    public GameObject please_find_the_blah_reminder;
    public UnityEngine.UI.Text please_find_the_blah_reminder_text;
    public GameObject deliver_item_visual_dislay;
    public UnityEngine.UI.Text deliver_item_display_text;
    public UnityEngine.UI.Text please_speak_now_text;
    public ScriptedEventReporter scriptedEventReporter;

    public IEnumerator DisplayLanguageMessage(GameObject[] language_messages)
    {
        yield return DisplayMessage(language_messages[(int)LanguageSource.current_language]);
    }

    public IEnumerator DisplayLanguageMessageFixedDuration(GameObject[] language_messages, float time, bool isFreeRecall)
    {
        if (!isFreeRecall)
        {
            yield return DisplayMessageWithoutX(language_messages[(int)LanguageSource.current_language], time);
        }
        else
        {
            yield return DisplayMessageWithoutX_Keypress(language_messages[(int)LanguageSource.current_language], time);
        }
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

    private IEnumerator DisplayMessageWithoutX_Keypress(GameObject message, float waitTime)
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
            float currTime = Time.time;

            if (Input.GetButtonDown("correct"))
            {
                string thisone = "correct" + i.ToString();
                data.Add(thisone, currTime);
                i++;
            } else if (Input.GetButtonDown("false"))
            {
                string thisone = "false" + i.ToString();
                data.Add(thisone, currTime);
                i++;
            } else if (Input.anyKeyDown)
            {
                string thisone = "invalid" + i.ToString();
                data.Add(thisone, currTime);
                i++;
            }
            
            yield return null;
        }
        scriptedEventReporter.ReportScriptedEvent("key press", data);
        scriptedEventReporter.ReportScriptedEvent("instruction message cleared", messageData);
        message.SetActive(false);
    }

    public void SetReminderText(string store_name)
    {
        string prompt_string = LanguageSource.GetLanguageString("please find prompt") + LanguageSource.GetLanguageString(store_name);
        please_find_the_blah_reminder_text.text = prompt_string;
    }

    public void SetDeliverItemText(string name)
    {
        string prompt_string = name;
        string update_name = "";
        foreach (char c in prompt_string)
        {
            if(char.IsLetter(c))
            {
                update_name += char.ToUpper(c);
            } else
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
