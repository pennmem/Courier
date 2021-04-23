using System;
using UnityEngine;

public sealed class WaitUntilWithTimeout : CustomYieldInstruction
{
    private readonly Func<bool> condition;
    private float timeout;

    public WaitUntilWithTimeout(Func<bool> condition, float timeout)
    {
        this.condition = condition;
        this.timeout = timeout;
    }

    public override bool keepWaiting
    {
        get
        {
            timeout -= Time.deltaTime;
            //if (timeout <= 0f)
                //throw new TimeoutException("WaitUntilWithTimeout timed out");
            return !condition() && (timeout > 0f);
        }
    }

    public bool timedOut() { return timeout <= 0f; }
}
