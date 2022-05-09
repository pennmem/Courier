using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

public class StarSystem : MonoBehaviour
{

    public RectTransform starCover;
    public UnityEngine.UI.Text numberText;

    private const float MAX_SCORE = 5.0f;
    private const int RECENT_AVERAGE_SIZE = 12;
    private const float numberDifferenceDisplayTime = 3f;

    private List<float> scores = new List<float>();
    private List<float> sessionGoodnesses = new List<float>();

    private float previousAverage = 0f;
    private float currentAverage = 0f;

    public bool ReportScore(float error, float goodness)
    {
        scores.Add(goodness * MAX_SCORE);
        sessionGoodnesses.Add(error);
        currentAverage = RecentAverage();

        UpdateCover(currentAverage / MAX_SCORE);

        return currentAverage > previousAverage;
    }

    public IEnumerator ShowDifference()
    {
        float changeInScore = currentAverage - previousAverage;
        string numberScore = numberText.text;
        numberText.text = numberScore + "\n" + changeInScore.ToString("+#.##;-#.##");
        yield return new WaitForSeconds(numberDifferenceDisplayTime);
        numberText.text = currentAverage.ToString("#.##");
        previousAverage = currentAverage;
    }

    public float CumulativeRating()
    {
        return scores.Sum() / scores.Count;
    }

    public int NumInSession()
    {
        return sessionGoodnesses.Count;
    }

    public int NumCorrectInSession(Func<float, bool> correctCond)
    {
        return sessionGoodnesses.Count(correctCond);
    }

    public void ResetSession()
    {
        sessionGoodnesses.Clear();
    }

    private float RecentAverage()
    {
        if (scores.Count == 0)
            return 0f;
        else if (scores.Count < RECENT_AVERAGE_SIZE)
            return scores.Sum() / scores.Count;
        else
            return scores.GetRange(scores.Count - RECENT_AVERAGE_SIZE, RECENT_AVERAGE_SIZE).Sum() / RECENT_AVERAGE_SIZE;
    }

	private void UpdateCover(float percentCover)
    {
        starCover.anchorMin = new Vector2(starCover.anchorMin.x, percentCover);
    }
}
