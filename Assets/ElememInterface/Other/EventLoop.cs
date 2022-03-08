using System;
using System.Threading;
using System.Collections.Concurrent;

public class EventLoop : EventQueue {
    private ManualResetEventSlim wait;

    ~EventLoop() {
        StopLoop();
    }

    public void StartLoop(){
        // spawn thread
        running = true;
        Thread loop = new Thread(Loop);
        wait = new ManualResetEventSlim();

        loop.Start();
    }

    public void StopLoop(){
        running = false;
        wait.Set();
        StopTimers();
    }

    public void Loop() {
        wait.Reset();
        while(Running()) {
            bool event_ran = Process();
            if ( ! event_ran ) {
                // Don't block indefinitely
                wait.Wait(200);
                wait.Reset();
            }
        }
        wait.Dispose();
    }
    public override void Do(IEventBase thisEvent) {
        base.Do(thisEvent);
        if(Running()) {
            wait.Set();
        }
    }

    // enqueues repeating event at set intervals. If timer isn't
    // stopped, stopping processing thread will still stop execution
    // of events
    public override void DoRepeating(RepeatingEvent thisEvent) {
        // timers should only be created if running
        if(Running()) {
            base.DoRepeating(thisEvent);
        } else {
            throw new Exception("Can't enqueue an event to a non running Loop");
        }
    }

    public EventLoop() {
        running = false;
    }
}
