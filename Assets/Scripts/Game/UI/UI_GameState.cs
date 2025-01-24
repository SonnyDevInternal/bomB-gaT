using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public struct Event
{
    public string text;

    public float showTime;
    public float holdTime;
    public float hideTime;
}

public class UI_GameState : MonoBehaviour
{
    enum EventState
    {
        Showing,
        Holding, 
        Hiding
    }

    private Queue<Event> Events = new Queue<Event>();

    private Image GameStateBar = null;
    private TextMeshProUGUI GameStateText = null;

    private Color GameStateColor = Color.white;
    private Color GameStateTextColor = Color.white;

    private float eventShowTime = 0.0f;
    private float eventHoldTime = 0.0f;
    private float eventHideTime = 0.0f;

    private float eventShowTimeCur = 0.0f;
    private float eventHoldTimeCur = 0.0f;
    private float eventHideTimeCur = 0.0f;

    private bool hasEvent = false;
    private bool isIntialized = false;

    private EventState currentState = EventState.Showing;

    private void Start()
    {
        Intialize();
    }


    private void Update()
    {
        if (!isIntialized)
            return;

        if(hasEvent)
        {
            UpdateShowEvent();
        }
        else
        {
            if(Events.Count > 0)
            {
                Event poppedEvent = Events.Dequeue();

                ExecuteEventInternal(poppedEvent.text, poppedEvent.showTime, poppedEvent.holdTime, poppedEvent.hideTime);
            }
        }
    }

    private void UpdateShowEvent()
    {
        switch(currentState)
        {
            case EventState.Showing:
                if (eventShowTimeCur >= eventShowTime)
                {
                    float alpha = 1.0f;

                    Color gamebarColor = GameStateBar.color;
                    Color gamestateTextColor = GameStateText.color;

                    GameStateBar.color = new Color(gamebarColor.r, gamebarColor.g, gamebarColor.b, alpha);
                    GameStateText.color = new Color(gamestateTextColor.r, gamestateTextColor.g, gamestateTextColor.b, alpha);

                    currentState = EventState.Holding;
                }
                else
                {
                    float alpha = eventShowTimeCur / eventShowTime;

                    SetUIAlpha(alpha);

                    eventShowTimeCur += Time.deltaTime;
                }

            break;

            case EventState.Holding:
                if (eventHoldTimeCur >= eventHoldTime)
                {
                    currentState = EventState.Hiding;
                }
                else
                    eventHoldTimeCur += Time.deltaTime;

            break;

            case EventState.Hiding:
                if (eventHideTimeCur >= eventHideTime)
                {
                    float alpha = 0.0f;

                    SetUIAlpha(alpha);

                    ResetShowEvent();
                }
                else
                {
                    float alpha = 1.0f - eventHideTimeCur / eventHideTime;

                    SetUIAlpha(alpha);

                    eventHideTimeCur += Time.deltaTime;
                }
                break;
        }
    }

    private void ResetShowEvent()
    {
        GameStateText.text = "";

        eventShowTime = 0.0f;
        eventHoldTime = 0.0f;
        eventHideTime = 0.0f;

        eventShowTimeCur = 0.0f;
        eventHoldTimeCur = 0.0f;
        eventHideTimeCur = 0.0f;

        hasEvent = false;

        currentState = EventState.Showing;
    }

    private void SetUIAlpha(float alpha)
    {
        GameStateBar.color = new Color(GameStateColor.r, GameStateColor.g, GameStateColor.b, alpha);
        GameStateText.color = new Color(GameStateTextColor.r, GameStateTextColor.g, GameStateTextColor.b, alpha);
    }


    private void ExecuteEventInternal(string text, float showTime, float holdTime, float hideTime)
    {
        ResetShowEvent();

        GameStateText.text = text;

        eventShowTime = showTime;
        eventHoldTime = holdTime;
        eventHideTime = hideTime;

        hasEvent = true;
    }

    public void ExecuteEvent(string text, float showTime, float holdTime, float hideTime) //Places an Event in the Queue System that later gets called
    {
        Event newEvent = new Event();

        newEvent.text = text;

        newEvent.showTime = showTime;
        newEvent.holdTime = holdTime;
        newEvent.hideTime = hideTime;

        Events.Enqueue(newEvent);
    }

    public void ForceEvent(string text, float showTime, float holdTime, float hideTime) //Skips Queue System and places itself in the front
    {
        ResetShowEvent();

        ExecuteEventInternal(text, showTime, holdTime, hideTime);
    }

    public void UpdateColor() //Call when you updat the color of either the GameBar or the Text
    {
        GameStateColor = GameStateBar.color;
        GameStateTextColor = GameStateText.color;
    }

    public void Intialize()
    {
        isIntialized = true;

        GameStateBar = transform.parent.GetComponent<Image>();
        GameStateText = GetComponent<TextMeshProUGUI>();

        ResetShowEvent();

        UpdateColor();

        SetUIAlpha(0.0f);
    }
}
