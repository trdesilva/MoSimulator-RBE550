using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public class AutoDriver : MonoBehaviour
{
    private Gamepad gamepad;

    // Start is called before the first frame update
    void Start()
    {
        Debug.LogWarning("AutoDriver started");
        gamepad = InputSystem.GetDevice<Gamepad>("autopad");
        if(gamepad == null)
        {
            gamepad = InputSystem.AddDevice<Gamepad>("autopad"); // persists as long as the process is alive
        }
    }

    // Update is called once per frame
    void Update()
    {
        // simulate 100% up input on the left analog stick
        using (StateEvent.From(gamepad, out InputEventPtr eventPtr))
        {
            gamepad["leftStick/y"].WriteValueIntoEvent<float>(1, eventPtr);
            InputSystem.QueueEvent(eventPtr);
        }
    }
}
