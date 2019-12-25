﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;

public class FollowingOrders : MonoBehaviour {
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;

    public KMSelectable[] ArrowButtons;
    public KMSelectable Switch;

    public Renderer[] Stones;
    public Renderer[] ColumnLights;
    public Renderer[] RowLights;
    public Renderer[] ColumnHieroglyphs;
    public Renderer[] RowHieroglyphs;

    public Material[] LightMarkers;
    public Material[] StoneMarkers;
    public Material[] Hieroglyphs;

    // Logging info
    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved = false;

    // Solving info
    private char firstSerialChar = '0';
    private char secondSerialChar = '0';
    private int serialDirection = 0;

    private string[] columnGlyphs = new string[5];
    private string[] rowGlyphs = new string[5];

    private readonly string[] HIEROGLYPH_WORDS = { "Ankh", "Cloth", "Cup", "Sieve", "Vulture" };
    private readonly string[] VOICES = { "Female", "Male", "Child" };
    private readonly string[] WORDS = { "Up", "Right", "Down", "Left" };

    private int[][] grid = { new int[5], new int[5], new int[5], new int[5], new int[5] };
    private int[][] gridIndex = { new int[5], new int[5], new int[5], new int[5], new int[5] };

    private int[] position = new int[2];
    private int[] goal = new int[2];

    private bool isPlaying = false;
    private bool canRestart = true;
    private bool firstTimeShouts = true;
    private bool isUnicorn = false;
    private bool canMove = false;

    private Shout[] shouts = new Shout[5];
    private int shoutCount = 3;

    private string desiredDirection = "Up";
    private string desiredHieroglyph = "Ankh";

    // Ran as bomb loads
    private void Awake() {
        moduleId = moduleIdCounter++;

        // Delegation
        Switch.OnInteract += delegate () { FlipSwitch(); return false; };

        for (int i = 0; i < ArrowButtons.Length; i++) {
            int j = i;
            ArrowButtons[i].OnInteract += delegate () { ArrowPressed(j); return false; };
        }
    }

    // Gets information
    private void Start() {
        // Gets the first and second characters of the serial number
        char[] serialNumber = Bomb.GetSerialNumber().ToCharArray();
        firstSerialChar = serialNumber[0];
        secondSerialChar = serialNumber[1];

        // Gets the direction indicated by the serial number
        if (Char.IsNumber(firstSerialChar) == true && Char.IsNumber(secondSerialChar) == true)
            serialDirection = 0; // Up

        else if (Char.IsNumber(firstSerialChar) == false && Char.IsNumber(secondSerialChar) == false)
            serialDirection = 1; // Right

        else if (Char.IsNumber(firstSerialChar) == false && Char.IsNumber(secondSerialChar) == true)
            serialDirection = 2; // Down

        else
            serialDirection = 3; // Left

        // Sets the grid index to values that correspond to Stones[]
        for (int i = 0; i < 5; i++) {
            int counter = 0;
            for (int j = i; counter < 5; j = j + 5) {
                gridIndex[i][counter] = j;
                counter++;
            }
        }

        GenerateMaze();

        // Once in a blue moon
        int random = UnityEngine.Random.Range(0, 1000);
        if (random == 0)
            isUnicorn = true;
    }


    // Generates maze
    private void GenerateMaze() {
        bool validMaze = false;

        do {
            // Sets each stone to a value for the random number generator to choose from
            for (int i = 0; i < 5; i++) {
                for (int j = 0; j < 5; j++) {
                    grid[i][j] = gridIndex[i][j] + 1;
                }
            }

            int stonesRemaining = 25;

            // Sets the trapped stones
            for (int traps = 0; traps < 10; traps++) {
                // Gets a value of where to put the trapped stone
                int random = UnityEngine.Random.Range(1, stonesRemaining + 1);
                stonesRemaining--;

                // Finds the stone that has that number and relaces it with a trap
                for (int i = 0; i < 5; i++) {
                    for (int j = 0; j < 5; j++) {
                        if (grid[i][j] == random)
                            grid[i][j] = -1;

                        if (grid[i][j] > random)
                            grid[i][j]--;
                    }
                }
            }

            // Creates a backup of the current grid
            int[][] gridBackup = { new int[5], new int[5], new int[5], new int[5], new int[5] };

            for (int i = 0; i < 5; i++) {
                for (int j = 0; j < 5; j++)
                    gridBackup[i][j] = grid[i][j];
            }

            // Selects starting position - if it can't find a good position then regenerate the maze
            for (int attempts = 1; attempts <= 15 && validMaze == false; attempts++) {
                // Restores the backup of the grid
                for (int i = 0; i < 5; i++) {
                    for (int j = 0; j < 5; j++)
                        grid[i][j] = gridBackup[i][j];
                }

                int startPos = attempts; //change this later

                // Sets the position into the grid
                for (int i = 0; i < 5; i++) {
                    for (int j = 0; j < 5; j++) {
                        if (grid[i][j] == startPos) {
                            grid[i][j] = 0;
                            position[0] = i;
                            position[1] = j;
                        }

                        else if (grid[i][j] != -1)
                            grid[i][j] = 20;
                    }
                }

                // Finds the largest distance from the starting position
                int largestValue = 0;

                for (int number = 1; number <= 14; number++) {
                    for (int i = 0; i < 5; i++) {
                        for (int j = 0; j < 5; j++) {
                            if (grid[i][j] == 20) {
                                // Finds the next lowest number value
                                if (j != 0 && grid[i][j - 1] == number - 1) { // Up
                                    grid[i][j] = number;
                                    largestValue = number;
                                }

                                if (i != 4 && grid[i + 1][j] == number - 1) { // Right
                                    grid[i][j] = number;
                                    largestValue = number;
                                }

                                if (j != 4 && grid[i][j + 1] == number - 1) { // Down
                                    grid[i][j] = number;
                                    largestValue = number;
                                }

                                if (i != 0 && grid[i - 1][j] == number - 1) { // Left
                                    grid[i][j] = number;
                                    largestValue = number;
                                }
                            }
                        }
                    }
                }

                // If there exists a tile is at least 5 tiles away from the starting location
                if (largestValue >= 5) {
                    validMaze = true;

                    // Sets the goal position as the farthest tile away
                    for (int i = 0; i < 5; i++) {
                        for (int j = 0; j < 5; j++) {
                            if (grid[i][j] == largestValue) { //change this later
                                goal[0] = i;
                                goal[1] = j;
                            }
                        }
                    }
                }
            }
        } while (validMaze == false);


        // Sets the goal position onto the grid
        for (int i = 0; i < 5; i++) {
            for (int j = 0; j < 5; j++) {
                if (goal[0] == i && goal[1] == j)
                    grid[i][j] = 0;

                else if (grid[i][j] != -1)
                    grid[i][j] = 20;
            }
        }

        // Determines how far tiles are away from goal
        for (int number = 1; number <= 14; number++) {
            for (int i = 0; i < 5; i++) {
                for (int j = 0; j < 5; j++) {
                    if (grid[i][j] == 20) {
                        // Finds the next lowest number value
                        if (j != 0 && grid[i][j - 1] == number - 1) // Up
                            grid[i][j] = number;

                        if (i != 4 && grid[i + 1][j] == number - 1) // Right
                            grid[i][j] = number;

                        if (j != 4 && grid[i][j + 1] == number - 1) // Down
                            grid[i][j] = number;

                        if (i != 0 && grid[i - 1][j] == number - 1) // Left
                            grid[i][j] = number;
                    }
                }
            }
        }

        // Sets the hieroglyphs
        for (int selection = 0; selection < 2; selection++) {
            int[] availableNumbers = { 0, 1, 2, 3, 4 };
            int[] chosenNumbers = new int[5];

            for (int numbersLeft = 5; numbersLeft > 0; numbersLeft--) {
                int random = UnityEngine.Random.Range(0, numbersLeft);
                
                for (int i = 0; i < HIEROGLYPH_WORDS.Length; i++) {
                    if (availableNumbers[i] == random) {
                        chosenNumbers[5 - numbersLeft] = i;
                        availableNumbers[i] = -1;
                    }

                    else if (availableNumbers[i] > random)
                        availableNumbers[i]--;
                }
            }

            if (selection == 0) { // Column
                for (int i = 0; i < columnGlyphs.Length; i++) {
                    columnGlyphs[i] = HIEROGLYPH_WORDS[chosenNumbers[i]];
                    ColumnHieroglyphs[i].material = Hieroglyphs[chosenNumbers[i]];
                }
            }

            else { // Row
                for (int i = 0; i < rowGlyphs.Length; i++) {
                    rowGlyphs[i] = HIEROGLYPH_WORDS[chosenNumbers[i]];
                    RowHieroglyphs[i].material = Hieroglyphs[chosenNumbers[i]];
                }
            }
        }

        UpdatePosition();


        // Logging the grid
        string[] gridLogger = new string[25];
        string[] gridLoggerMsg = new string[5];
        
        // Gets the traps from the grid
        for (int i = 0; i < 5; i++) {
            for (int j = 0; j < 5; j++) {
                if (grid[i][j] == -1)
                    gridLogger[gridIndex[i][j]] = "*";
                
                else
                    gridLogger[gridIndex[i][j]] = ".";
            }
        }

        // Shortens the information into rows
        for (int i = 0; i < gridLoggerMsg.Length; i++) {
            gridLoggerMsg[i] = gridLogger[i * 5] + " " + gridLogger[i * 5 + 1] + " " + gridLogger[i * 5 + 2] + " " +
                gridLogger[i * 5 + 3] + " " + gridLogger[i * 5 + 4];
        }

        Debug.LogFormat("[Following Orders #{0}] The maze generated as such, where '.' represents a safe tile and '*' represents a trapped tile: \n{1}\n{2}\n{3}\n{4}\n{5}", moduleId, 
            gridLoggerMsg[0], gridLoggerMsg[1], gridLoggerMsg[2], gridLoggerMsg[3], gridLoggerMsg[4]);

        Debug.LogFormat("[Following Orders #{0}] Your starting position is column {1}, row {2}.", moduleId, position[0] + 1, position[1] + 1);
        Debug.LogFormat("[Following Orders #{0}] Your goal is column {1}, row {2}.", moduleId, goal[0] + 1, goal[1] + 1);

        Debug.LogFormat("[Following Orders #{0}] Column Hieroglyphs: {1}, {2}, {3}, {4}, {5}", moduleId, 
            columnGlyphs[0], columnGlyphs[1], columnGlyphs[2], columnGlyphs[3], columnGlyphs[4]);

        Debug.LogFormat("[Following Orders #{0}] Row Hieroglyphs: {1}, {2}, {3}, {4}, {5}", moduleId,
            rowGlyphs[0], rowGlyphs[1], rowGlyphs[2], rowGlyphs[3], rowGlyphs[4]);

        canMove = true;
    }


    // Switch is flipped
    private void FlipSwitch() {
        Switch.AddInteractionPunch();
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, gameObject.transform);

        if (moduleSolved == false) {
            if (isPlaying == true)
                isPlaying = false;

            else {
                isPlaying = true;

                if (canRestart == true)
                    StartCoroutine(DisplayShouts());
            }
        }
    }

    // Directional arrow is pressed
    private void ArrowPressed(int direction) {
        ArrowButtons[direction].AddInteractionPunch(0.5f);
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, gameObject.transform);

        if (moduleSolved == false && canMove == true) {
            /* 0 = Up
             * 1 = Right
             * 2 = Down
             * 3 = Left
             */
            switch(direction) {
            case 0: {
                if (position[1] != 0) {
                    position[1]--;
                    UpdatePosition();
                }
            }
            break;

            case 1: {
                if (position[0] != 4) {
                    position[0]++;
                    UpdatePosition();
                }
            }
            break;

            case 2: {
                if (position[1] != 4) {
                    position[1]++;
                    UpdatePosition();
                }
            }
            break;

            default: {
                if (position[0] != 0) {
                    position[0]--;
                    UpdatePosition();
                }
            }
            break;
            }
        }
    }


    // Shouts being shouted
    private IEnumerator DisplayShouts() {
        canRestart = false;

        // Sets the shouts to a different variable so they don't get rewritten during moving
        Shout[] tempShouts = new Shout[shouts.Length];
        string tempDirection = desiredDirection;
        string tempHieroglyph = desiredHieroglyph;

        for (int i = 0; i < shouts.Length; i++) {
            tempShouts[i] = shouts[i];
        }

        // Logs the desired destination if this is the first time hearing the shouts on this tile
        if (firstTimeShouts == true) {
            firstTimeShouts = false;
            Debug.LogFormat("[Following Orders #{0}] Desired destination from column {1}, row {2}: {3} to {4}.", moduleId, 
                position[0] + 1, position[1] + 1, desiredDirection, desiredHieroglyph);
        }

        if (isUnicorn == true) {
            switch(tempDirection) {
            case "Right": {
                Audio.PlaySoundAtTransform("FollowingOrders_MSDir_Right", transform);
            }
            break;

            case "Down": {
                Audio.PlaySoundAtTransform("FollowingOrders_MSDir_Down", transform);
            }
            break;

            case "Left": {
                Audio.PlaySoundAtTransform("FollowingOrders_MSDir_Left", transform);
            }
            break;

            default: {
                Audio.PlaySoundAtTransform("FollowingOrders_MSDir_Up", transform);
            }
            break;
            }

            yield return new WaitForSeconds(1.0f);

            switch(tempHieroglyph) {
            case "Cloth": {
                Audio.PlaySoundAtTransform("FollowingOrders_MSHei_Cloth", transform);
            }
            break;

            case "Cup": {
                Audio.PlaySoundAtTransform("FollowingOrders_MSHei_Cup", transform);
            }
            break;

            case "Sieve": {
                Audio.PlaySoundAtTransform("FollowingOrders_MSHei_Sieve", transform);
            }
            break;

            case "Vulture": {
                Audio.PlaySoundAtTransform("FollowingOrders_MSHei_Vulture", transform);
            }
            break;

            default: {
                Audio.PlaySoundAtTransform("FollowingOrders_MSHei_Ankh", transform);
            }
            break;
            }
        }

        else {
            for (int i = 0; i < shoutCount; i++) {
                PlayStandardShout(tempShouts[i]);
                yield return new WaitForSeconds(1.5f);
            }
        }

        yield return new WaitForSeconds(2.5f);
        canRestart = true;
        if (isPlaying == true)
            StartCoroutine(DisplayShouts());
    }

    // Chooses the sound for the shout
    private void PlayStandardShout(Shout shout) {
        if (shout.GetVoice() == VOICES[2]) { // Child
            switch (shout.GetDirection()) {
            case "Right": {
                Audio.PlaySoundAtTransform("FollowingOrders_Child_Right", transform);
            }
            break;

            case "Down": {
                Audio.PlaySoundAtTransform("FollowingOrders_Child_Down", transform);
            }
            break;

            case "Left": {
                Audio.PlaySoundAtTransform("FollowingOrders_Child_Left", transform);
            }
            break;

            default: {
                Audio.PlaySoundAtTransform("FollowingOrders_Child_Up", transform);
            }
            break;
            }
        }

        else if (shout.GetVoice() == VOICES[1]) { // Male
            switch (shout.GetDirection()) {
            case "Right": {
                Audio.PlaySoundAtTransform("FollowingOrders_Male_Right", transform);
            }
            break;

            case "Down": {
                Audio.PlaySoundAtTransform("FollowingOrders_Male_Down", transform);
            }
            break;

            case "Left": {
                Audio.PlaySoundAtTransform("FollowingOrders_Male_Left", transform);
            }
            break;

            default: {
                Audio.PlaySoundAtTransform("FollowingOrders_Male_Up", transform);
            }
            break;
            }
        }

        else { // Female
            switch (shout.GetDirection()) {
            case "Right": {
                Audio.PlaySoundAtTransform("FollowingOrders_Female_Right", transform);
            }
            break;

            case "Down": {
                Audio.PlaySoundAtTransform("FollowingOrders_Female_Down", transform);
            }
            break;

            case "Left": {
                Audio.PlaySoundAtTransform("FollowingOrders_Female_Left", transform);
            }
            break;

            default: {
                Audio.PlaySoundAtTransform("FollowingOrders_Female_Up", transform);
            }
            break;
            }
        }
    }


    // Updates your current position
    private void UpdatePosition() {
        isPlaying = false;
        firstTimeShouts = true;

        // Clears all yellow lights
        for (int i = 0; i < ColumnLights.Length; i++) {
            ColumnLights[i].material = LightMarkers[0];
            RowLights[i].material = LightMarkers[0];
        }

        // Sets your position to yellow lights
        ColumnLights[position[0]].material = LightMarkers[1];
        RowLights[position[1]].material = LightMarkers[1];

        // Checks if the position is the goal
        if (position[0] == goal[0] && position[1] == goal[1])
            Solve();

        // checks if the position is a trap
        else if (grid[position[0]][position[1]] == -1)
            StartCoroutine(Strike());

        // Gets the shouts from the tile
        else
            GenerateShouts();
    }


    // Module solved
    private void Solve() {
        canMove = false;
        moduleSolved = true;
        DisplayLEDColor(3);
        GetComponent<KMBombModule>().HandlePass();
        Audio.PlaySoundAtTransform("FollowingOrders_Solve", transform);
        Debug.LogFormat("[Following Orders #{0}] Module solved!", moduleId);
    }

    // Module struck
    private IEnumerator Strike() {
        canMove = false;
        DisplayLEDColor(2);
        ShowRedStones();
        GetComponent<KMBombModule>().HandleStrike();
        Debug.LogFormat("[Following Orders #{0}] Strike! Generating new maze...", moduleId);

        yield return new WaitForSeconds(1.5f);
        GenerateMaze();
        ClearStoneColors();
    }


    // Shows the LEDs as a certain color
    private void DisplayLEDColor(int color) {
        /* 0 = White
         * 1 = Yellow
         * 2 = Red
         * 3 = Green
         */

        for (int i = 0; i < ColumnLights.Length; i++) {
            ColumnLights[i].material = LightMarkers[color];
            RowLights[i].material = LightMarkers[color];
        }
    }

    // Shows the trapped stones as red
    private void ShowRedStones() {
        for (int i = 0; i < 5; i++) {
            for (int j = 0; j < 5; j++) {
                if (grid[i][j] == -1)
                    Stones[gridIndex[i][j]].material = StoneMarkers[1];
            }
        }
    }

    // Clears the color of the stones
    private void ClearStoneColors() {
        for (int i = 0; i < 5; i++) {
            for (int j = 0; j < 5; j++)
                Stones[gridIndex[i][j]].material = StoneMarkers[0];
        }
    }


    // Generates the shouts
    private void GenerateShouts() {
        bool validShouts = false;
        isUnicorn = true;//testing purposes only
        GetDestination();

        if (isUnicorn == true) {
            validShouts = true;
        }

        //while (validShouts == false) {
        //
        //}
    }

    // Gets the desired destination
    private void GetDestination() {
        int smallestValue = grid[position[0]][position[1]];

        /* 0 = Up
         * 1 = Right
         * 2 = Down
         * 3 = Right
         */

        for (int i = position[0]; i <= 4; i++) { // Right
            if (grid[i][position[1]] == -1)
                break;

            else if (grid[i][position[1]] < smallestValue) {
                smallestValue = grid[i][position[1]];
                desiredDirection = WORDS[1];
                desiredHieroglyph = columnGlyphs[i];
            }
        }

        for (int j = position[1]; j <= 4; j++) { // Down
            if (grid[position[0]][j] == -1)
                break;

            else if (grid[position[0]][j] < smallestValue) {
                smallestValue = grid[position[0]][j];
                desiredDirection = WORDS[2];
                desiredHieroglyph = rowGlyphs[j];
            }
        }

        for (int i = position[0]; i >= 0; i--) { // Left
            if (grid[i][position[1]] == -1)
                break;

            else if (grid[i][position[1]] < smallestValue) {
                smallestValue = grid[i][position[1]];
                desiredDirection = WORDS[3];
                desiredHieroglyph = columnGlyphs[i];
            }
        }

        for (int j = position[1]; j >= 0; j--) { // Up
            if (grid[position[0]][j] == -1)
                break;

            else if (grid[position[0]][j] < smallestValue) {
                smallestValue = grid[position[0]][j];
                desiredDirection = WORDS[0];
                desiredHieroglyph = rowGlyphs[j];
            }
        }
    }
}