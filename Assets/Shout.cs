using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class Shout {
    private string voice;
    private string direction;
    private bool present;

    public Shout() {
        voice = "Male";
        direction = "Up";
        present = false;
    }

    public Shout(string voice, string direction, bool present) {
        this.voice = voice;
        this.direction = direction;
        this.present = present;
    }

    public void SetVoice(string voice) {
        this.voice = voice;
    }

    public void SetPresent(bool present) {
        this.present = present;
    }

    public void SetDirection(string direction) {
        this.direction = direction;
    }

    public string GetVoice() {
        return voice;
    }

    public string GetDirection() {
        return direction;
    }

    public bool GetPresent() {
        return present;
    }
}