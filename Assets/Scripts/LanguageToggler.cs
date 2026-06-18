using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LanguageToggler : MonoBehaviour
{
    public GameObject[] hideUsOnGerman;
    public UnityEngine.UI.Toggle toggleMeOnGerman;

    public void SetCurrentLanguage(int language)
    {
        // German is deprecated: the German store audio has been removed from the project,
        // so force English regardless of what the participant selects. This guarantees the
        // germanAudio[] arrays (now empty/missing) are never read at runtime.
        LanguageSource.current_language = LanguageSource.LANGUAGE.ENGLISH;
        toggleMeOnGerman.isOn = false;
        foreach (GameObject us in hideUsOnGerman)
        {
            us.SetActive(true);
        }
    }
}