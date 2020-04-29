using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;

/* Things to still do:
 * Find child's voices that are actually shouts, not samples from kid's songs
 * Make it so the goal doesn't choose the first tile that's the farthest away in case of a tie for farthest away
 */

public class FollowingOrders : MonoBehaviour {
    public KMAudio Audio;
    public KMBombInfo Bomb;
    public KMBombModule Module;

    public KMSelectable[] ArrowButtons;
    public KMSelectable Switch;

    public Transform speakerPos;

    public Renderer[] SwitchModels;
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
    private string serialDirection = "Up";

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
    private bool switchState = false;

    private Shout[] shouts = new Shout[5];
    private int shoutCount = 3;

    int[] voiceCount = { 0, 0, 0 };
    int[] directionCount = { 0, 0, 0, 0 };

    int[] femaleDirectionCount = { 0, 0, 0, 0 };
    int[] maleDirectionCount = { 0, 0, 0, 0 };
    int[] childDirectionCount = { 0, 0, 0, 0 };

    private string desiredDirection = "Up";
    private string desiredHieroglyph = "Ankh";

    private readonly int SHOUT_ATTEMPT_MAX = 100000; // Once in a blue moon


    // Testing mode
    private bool testMode = false; // Make this true for test mode

    private string testSerialDirection = "Left";

    private int[] testGrid = { -1, -1, 1, -1, 2,
                               3, 4, 5, 6, 7,
                               -1, 8, 9, 10, -1,
                               -1, -1, -1, -1, 11,
                               12, 13, 14, -1, 15}; // -1 = trap, 1++ = safe

    private int testStartPos = 7;
    private int[] testGoalPos = { 1, 2 };

    private int[] testColumnGlyphs = { 0, 4, 3, 1, 2 };
    private int[] testRowGlyphs = { 1, 3, 0, 4, 2 };
    /* 0 = Ankh
     * 1 = Cloth
     * 2 = Cup
     * 3 = Sieve
     * 4 = Vulture
     */

    private int testShoutCount = 5;

    private int[] testShoutVoices = { 2, 1, 1, 0, 1 };
    /* 0: Female
     * 1: Male
     * 2: Child
     */

    private int[] testShoutDirections = { 0, 2, 0, 3, 0 };
    /* 0: Up
     * 1: Right
     * 2: Down
     * 3: Left
     */


    // Ran as bomb loads
    private void Awake() {
        moduleId = moduleIdCounter++;

        // Delegation
        Switch.OnInteract += delegate () { FlipSwitch(!switchState); return false; };

        for (int i = 0; i < ArrowButtons.Length; i++) {
            int j = i;
            ArrowButtons[i].OnInteract += delegate () { ArrowPressed(j); return false; };
        }
    }

    // Gets information
    private void Start() {

        // Logs if test mode is turned on.
        if (testMode == true)
            Debug.LogFormat("[Following Orders #{0}] Test mode is turned on.", moduleId);

        // Gets the first and second characters of the serial number
        char[] serialNumber = Bomb.GetSerialNumber().ToCharArray();
        firstSerialChar = serialNumber[0];
        secondSerialChar = serialNumber[1];

        // Gets the direction indicated by the serial number
        if (testMode == false) {
            if (Char.IsNumber(firstSerialChar) == true && Char.IsNumber(secondSerialChar) == true)
                serialDirection = WORDS[0]; // Up

            else if (Char.IsNumber(firstSerialChar) == false && Char.IsNumber(secondSerialChar) == false)
                serialDirection = WORDS[1]; // Right

            else if (Char.IsNumber(firstSerialChar) == false && Char.IsNumber(secondSerialChar) == true)
                serialDirection = WORDS[2]; // Down

            else
                serialDirection = WORDS[3]; // Left
        }

        else
            serialDirection = testSerialDirection;

        // Sets the grid index to values that correspond to Stones[]
        for (int i = 0; i < 5; i++) {
            int counter = 0;
            for (int j = i; counter < 5; j = j + 5) {
                gridIndex[i][counter] = j;
                counter++;
            }
        }

        GenerateMaze();

        SwitchModels[1].enabled = false;
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
            if (testMode == false) {
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
            }

            else {
                for (int i = 0; i < testGrid.Length; i++) {
                    grid[i % 5][i / 5] = testGrid[i];
                }
            }

            // Creates a backup of the current grid
            int[][] gridBackup = { new int[5], new int[5], new int[5], new int[5], new int[5] };

            for (int i = 0; i < 5; i++) {
                for (int j = 0; j < 5; j++)
                    gridBackup[i][j] = grid[i][j];
            }

            int[] availableNumbers = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 };

            // Selects starting position - if it can't find a good position then regenerate the maze

            for (int attempts = 1; attempts <= 15 && validMaze == false; attempts++) {
                // Restores the backup of the grid
                for (int i = 0; i < 5; i++) {
                    for (int j = 0; j < 5; j++)
                        grid[i][j] = gridBackup[i][j];
                }

                // Selects a starting position
                int startPos = attempts;

                if (testMode == false) {
                    int random = UnityEngine.Random.Range(1, stonesRemaining + 1);

                    for (int i = 0; i < availableNumbers.Length; i++) {
                        if (availableNumbers[i] == random) {
                            startPos = i;
                            availableNumbers[i] = -1;
                        }

                        else if (availableNumbers[i] > random)
                            availableNumbers[i]--;
                    }
                }

                else
                    startPos = testStartPos;

                stonesRemaining--;

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
                if (largestValue >= 5 || testMode == true) {
                    validMaze = true;

                    // Sets the goal position as the farthest tile away
                    if (testMode == false) {
                        for (int i = 0; i < 5; i++) {
                            for (int j = 0; j < 5; j++) {
                                if (grid[i][j] == largestValue) {
                                    goal[0] = i;
                                    goal[1] = j;
                                }
                            }
                        }
                    }

                    else {
                        goal[0] = testGoalPos[0];
                        goal[1] = testGoalPos[1];
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
        if (testMode == false) {
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
        }

        else {
            for (int i = 0; i < testColumnGlyphs.Length; i++) {
                columnGlyphs[i] = HIEROGLYPH_WORDS[testColumnGlyphs[i]];
                ColumnHieroglyphs[i].material = Hieroglyphs[testColumnGlyphs[i]];
                rowGlyphs[i] = HIEROGLYPH_WORDS[testRowGlyphs[i]];
                RowHieroglyphs[i].material = Hieroglyphs[testRowGlyphs[i]];
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
    private void FlipSwitch(bool state) {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Switch.transform);

        switchState = state;

        if (state == true) {
            SwitchModels[1].enabled = true;
            SwitchModels[0].enabled = false;
        }

        else {
            SwitchModels[0].enabled = true;
            SwitchModels[1].enabled = false;
        }


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
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, ArrowButtons[direction].transform);

        if (moduleSolved == false && canMove == true) {
            /* 0 = Up
             * 1 = Right
             * 2 = Down
             * 3 = Left
             */
            switch (direction) {
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
        bool logShouts = firstTimeShouts;

        // Generates the shouts if this is the first time hearing the shouts on this tile
        if (firstTimeShouts == true) {
            firstTimeShouts = false;
            GenerateShouts();
            Debug.LogFormat("[Following Orders #{0}] Desired destination from column {1}, row {2}: {3} to {4}.", moduleId,
                position[0] + 1, position[1] + 1, desiredDirection, desiredHieroglyph);
        }

        // Sets the shouts to a different variable so they don't get rewritten during moving
        Shout[] tempShouts = new Shout[shouts.Length];
        string tempDirection = desiredDirection;
        string tempHieroglyph = desiredHieroglyph;

        for (int i = 0; i < shouts.Length; i++) {
            tempShouts[i] = shouts[i];
        }

        yield return new WaitForSeconds(0.5f);

        if (isUnicorn == true) {
            if (logShouts == true)
                Debug.LogFormat("[Following Orders #{0}] Once in a blue moon, Microsoft Sam just told you the destination.", moduleId);

            switch (tempDirection) {
            case "Right": Audio.PlaySoundAtTransform("FollowingOrders_MSDir_Right", speakerPos); break;
            case "Down": Audio.PlaySoundAtTransform("FollowingOrders_MSDir_Down", speakerPos); break;
            case "Left": Audio.PlaySoundAtTransform("FollowingOrders_MSDir_Left", speakerPos); break;
            default: Audio.PlaySoundAtTransform("FollowingOrders_MSDir_Up", speakerPos); break;
            }

            yield return new WaitForSeconds(1.0f);

            switch (tempHieroglyph) {
            case "Cloth": Audio.PlaySoundAtTransform("FollowingOrders_MSHei_Cloth", speakerPos); break;
            case "Cup": Audio.PlaySoundAtTransform("FollowingOrders_MSHei_Cup", speakerPos); break;
            case "Sieve": Audio.PlaySoundAtTransform("FollowingOrders_MSHei_Sieve", speakerPos); break;
            case "Vulture": Audio.PlaySoundAtTransform("FollowingOrders_MSHei_Vulture", speakerPos); break;
            default: Audio.PlaySoundAtTransform("FollowingOrders_MSHei_Ankh", speakerPos); break;
            }
        }

        else {
            string shoutLog = "";

            for (int i = 0; i < shoutCount; i++) {
                shoutLog += PlayStandardShout(tempShouts[i], logShouts);

                if (i != shoutCount - 1)
                    shoutLog += ", ";

                yield return new WaitForSeconds(1.5f);
            }

            if (logShouts == true)
                Debug.LogFormat("[Following Orders #{0}] Shouts: {1}", moduleId, shoutLog);
        }

        yield return new WaitForSeconds(2.0f);
        canRestart = true;
        if (isPlaying == true)
            StartCoroutine(DisplayShouts());
    }

    // Chooses the sound for the shout
    private string PlayStandardShout(Shout shout, bool logShout) {
        string log = "";

        if (shout.GetVoice() == VOICES[2]) { // Child
            switch (shout.GetDirection()) {
            case "Right": Audio.PlaySoundAtTransform("FollowingOrders_Child_Right", speakerPos); log = "Child Right"; break;
            case "Down": Audio.PlaySoundAtTransform("FollowingOrders_Child_Down", speakerPos); log = "Child Down"; break;
            case "Left": Audio.PlaySoundAtTransform("FollowingOrders_Child_Left", speakerPos); log = "Child Left"; break;
            default: Audio.PlaySoundAtTransform("FollowingOrders_Child_Up", speakerPos); log = "Child Up"; break;
            }
        }

        else if (shout.GetVoice() == VOICES[1]) { // Male
            switch (shout.GetDirection()) {
            case "Right": Audio.PlaySoundAtTransform("FollowingOrders_Male_Right", speakerPos); log = "Male Right"; break;
            case "Down": Audio.PlaySoundAtTransform("FollowingOrders_Male_Down", speakerPos); log = "Male Down"; break;
            case "Left": Audio.PlaySoundAtTransform("FollowingOrders_Male_Left", speakerPos); log = "Male Left"; break;
            default: Audio.PlaySoundAtTransform("FollowingOrders_Male_Up", speakerPos); log = "Male Up"; break;
            }
        }

        else { // Female
            switch (shout.GetDirection()) {
            case "Right": Audio.PlaySoundAtTransform("FollowingOrders_Female_Right", speakerPos); log = "Female Right"; break;
            case "Down": Audio.PlaySoundAtTransform("FollowingOrders_Female_Down", speakerPos); log = "Female Down"; break;
            case "Left": Audio.PlaySoundAtTransform("FollowingOrders_Female_Left", speakerPos); log = "Female Left"; break;
            default: Audio.PlaySoundAtTransform("FollowingOrders_Female_Up", speakerPos); log = "Female Up"; break;
            }
        }

        // Logging the shouts
        if (logShout == true)
            return log;

        else
            return "";
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
    }


    // Module solved
    private void Solve() {
        canMove = false;
        moduleSolved = true;
        DisplayLEDColor(3);
        GetComponent<KMBombModule>().HandlePass();
        Audio.PlaySoundAtTransform("FollowingOrders_Solve", speakerPos);
        Debug.LogFormat("[Following Orders #{0}] Module solved! Walk like an Egyptian!", moduleId);
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
        GetDestination();
        isUnicorn = false;

        int shoutAttempts = 0; // Prevents infinite loops

        while (validShouts == false && shoutAttempts < SHOUT_ATTEMPT_MAX) {
            if (testMode == false)
                shoutCount = UnityEngine.Random.Range(3, 6);

            else
                shoutCount = testShoutCount;

            // Resets the counts of each direction and voice
            for (int i = 0; i < voiceCount.Length; i++) {
                voiceCount[i] = 0;
            }

            for (int i = 0; i < directionCount.Length; i++) {
                directionCount[i] = 0;
            }

            // Initializes shouts
            for (int i = 0; i < shouts.Length; i++)
                shouts[i] = new Shout();

            for (int i = 0; i < directionCount.Length; i++) {
                directionCount[i] = 0;
                femaleDirectionCount[i] = 0;
                maleDirectionCount[i] = 0;
                childDirectionCount[i] = 0;
            }

            for (int i = 0; i < voiceCount.Length; i++) {
                voiceCount[i] = 0;
            }

            // Chooses which shouts to use
            for (int i = 0; i < shoutCount; i++) {
                /* 0: Female
                 * 1: Male
                 * 2: Child
                 */

                int random = UnityEngine.Random.Range(0, 3);
                if (testMode == true)
                    random = testShoutVoices[i];

                shouts[i].SetVoice(VOICES[random]);
                voiceCount[random]++;

                /* 0: Up
                 * 1: Right
                 * 2: Down
                 * 3: Left
                 */

                random = UnityEngine.Random.Range(0, 4);
                if (testMode == true)
                    random = testShoutDirections[i];

                shouts[i].SetDirection(WORDS[random]);
                directionCount[random]++;

                if (shouts[i].GetVoice() == VOICES[2])
                    childDirectionCount[random]++;

                else if (shouts[i].GetVoice() == VOICES[1])
                    maleDirectionCount[random]++;

                else
                    femaleDirectionCount[random]++;

                shouts[i].SetPresent(true);
            }

            int[] columnValues = { 0, 0, 0, 0, 0 };
            int[] rowValues = { 0, 0, 0, 0, 0 };

            bool[] tableGrid = SetTableGridValues(new bool[25]);

            // Gets the counts of each of the true statements for the rows and columns
            for (int i = 0; i < tableGrid.Length; i++) {
                if (tableGrid[i] == true) {
                    columnValues[i % 5]++;
                    rowValues[i / 5]++;
                }
            }

            // Finds the hieroglyph from the table grid (I feel like there's a better way to write this code)
            string chosenHieroglyph = "";

            if (columnValues[0] > columnValues[1] && columnValues[0] > columnValues[2] &&
                columnValues[0] > columnValues[3] && columnValues[0] > columnValues[4])
                chosenHieroglyph = HIEROGLYPH_WORDS[0];

            else if (columnValues[1] > columnValues[0] && columnValues[1] > columnValues[2] &&
                columnValues[1] > columnValues[3] && columnValues[1] > columnValues[4])
                chosenHieroglyph = HIEROGLYPH_WORDS[1];

            else if (columnValues[2] > columnValues[0] && columnValues[2] > columnValues[1] &&
                columnValues[2] > columnValues[3] && columnValues[2] > columnValues[4])
                chosenHieroglyph = HIEROGLYPH_WORDS[2];

            else if (columnValues[3] > columnValues[0] && columnValues[3] > columnValues[1] &&
                columnValues[3] > columnValues[2] && columnValues[3] > columnValues[4])
                chosenHieroglyph = HIEROGLYPH_WORDS[3];

            else if (columnValues[4] > columnValues[0] && columnValues[4] > columnValues[1] &&
                columnValues[4] > columnValues[2] && columnValues[4] > columnValues[3])
                chosenHieroglyph = HIEROGLYPH_WORDS[4];

            else
                chosenHieroglyph = HIEROGLYPH_WORDS[position[0]];


            // If chosen hieroglyph does not match desired hieroglyph, regenerate the shouts
            if (chosenHieroglyph == desiredHieroglyph) {
                string chosenDirection = "";
                string closestToHieroglyph = "";
                int distance = 5;
                bool tied = true;

                // Finds the direction that's the closest to the hieroglyph
                int counter = 1;
                for (int j = position[1] - 1; j >= 0; j--) { // Up
                    if (rowGlyphs[j] == desiredHieroglyph) {
                        if (counter < distance) {
                            closestToHieroglyph = WORDS[0];
                            distance = counter;
                            tied = false;
                        }

                        else if (counter == distance)
                            tied = true;

                        break;
                    }

                    counter++;
                }

                counter = 1;
                for (int i = position[0] + 1; i <= 4; i++) { // Right
                    if (columnGlyphs[i] == desiredHieroglyph) {
                        if (counter < distance) {
                            closestToHieroglyph = WORDS[1];
                            distance = counter;
                            tied = false;
                        }

                        else if (counter == distance)
                            tied = true;

                        break;
                    }

                    counter++;
                }

                counter = 1;
                for (int j = position[1] + 1; j <= 4; j++) { // Down
                    if (rowGlyphs[j] == desiredHieroglyph) {
                        if (counter < distance) {
                            closestToHieroglyph = WORDS[2];
                            distance = counter;
                            tied = false;
                        }

                        else if (counter == distance)
                            tied = true;

                        break;
                    }

                    counter++;
                }

                counter = 1;
                for (int i = position[0] - 1; i >= 0; i--) { // Left
                    if (columnGlyphs[i] == desiredHieroglyph) {
                        if (counter < distance) {
                            closestToHieroglyph = WORDS[3];
                            distance = counter;
                            tied = false;
                        }

                        else if (counter == distance)
                            tied = true;

                        break;
                    }

                    counter++;
                }

                if (tied == true)
                    closestToHieroglyph = serialDirection;

                // Finds the direction from the table grid
                if (rowValues[0] > rowValues[1] && rowValues[0] > rowValues[2] &&
                rowValues[0] > rowValues[3] && rowValues[0] > rowValues[4])
                    chosenDirection = WORDS[0];

                else if (rowValues[1] > rowValues[0] && rowValues[1] > rowValues[2] &&
                    rowValues[1] > rowValues[3] && rowValues[1] > rowValues[4])
                    chosenDirection = WORDS[3];

                else if (rowValues[2] > rowValues[0] && rowValues[2] > rowValues[1] &&
                    rowValues[2] > rowValues[3] && rowValues[2] > rowValues[4])
                    chosenDirection = WORDS[2];

                else if (rowValues[3] > rowValues[0] && rowValues[3] > rowValues[1] &&
                    rowValues[3] > rowValues[2] && rowValues[3] > rowValues[4])
                    chosenDirection = WORDS[1];

                else if (rowValues[4] > rowValues[0] && rowValues[4] > rowValues[1] &&
                    rowValues[4] > rowValues[2] && rowValues[4] > rowValues[3])
                    chosenDirection = closestToHieroglyph;

                else {
                    switch (position[1]) {
                    case 1: chosenDirection = WORDS[3]; break;
                    case 2: chosenDirection = WORDS[2]; break;
                    case 3: chosenDirection = WORDS[1]; break;
                    case 4: chosenDirection = closestToHieroglyph; break;
                    default: chosenDirection = WORDS[0]; break;
                    }
                }

                // If chosen direction does not match desired direction, regenerate the shouts
                if (chosenDirection == desiredDirection)
                    validShouts = true;
            }

            shoutAttempts++;
        }

        if (shoutCount >= SHOUT_ATTEMPT_MAX || validShouts == false)
            isUnicorn = true;
    }

    // Sets the values for the table grid
    private bool[] SetTableGridValues(bool[] tableGrid) {
        // Initializes all the values in the table grid
        for (int i = 0; i < tableGrid.Length; i++) {
            tableGrid[i] = false;
        }

        tableGrid[4] = true;

        for (int i = 0; i < shoutCount; i++) {
            if (i != shoutCount - 1) {
                if (shouts[i].GetVoice() == VOICES[1] && shouts[i + 1].GetDirection() == WORDS[3])
                    tableGrid[2] = true;
            }

            if (i != 0) {
                if (shouts[i].GetDirection() != shouts[i - 1].GetDirection())
                    tableGrid[4] = false;
            }

            if (i > 1) {
                if (shouts[i].GetDirection() == shouts[i - 2].GetDirection())
                    tableGrid[14] = true;
            }
        }

        if (rowGlyphs[position[1]] == HIEROGLYPH_WORDS[2])
            tableGrid[0] = true;

        if (voiceCount[0] > 0)
            tableGrid[1] = true;

        if (directionCount[0] == 0 && directionCount[2] == 0)
            tableGrid[3] = true;

        if (directionCount[1] >= 2)
            tableGrid[5] = true;

        if (directionCount[3] == 0)
            tableGrid[6] = true;

        if (voiceCount[0] >= 2)
            tableGrid[7] = true;

        if (columnGlyphs[position[0]] == HIEROGLYPH_WORDS[4])
            tableGrid[8] = true;

        if (directionCount[2] >= 1)
            tableGrid[9] = true;

        if (voiceCount[1] == 0)
            tableGrid[10] = true;

        if (columnGlyphs[position[0]] == HIEROGLYPH_WORDS[0])
            tableGrid[11] = true;

        if (directionCount[3] >= 1)
            tableGrid[12] = true;

        if (voiceCount[2] > 0)
            tableGrid[13] = true;

        if (directionCount[0] >= 3 || directionCount[1] >= 3 || directionCount[2] >= 3 || directionCount[3] >= 3)
            tableGrid[15] = true;

        if (voiceCount[1] > 0)
            tableGrid[16] = true;

        if (voiceCount[0] > 0 && voiceCount[1] > 0 && voiceCount[2] == 0)
            tableGrid[17] = true;

        if (shoutCount == 3)
            tableGrid[18] = true;

        if (rowGlyphs[position[1]] == HIEROGLYPH_WORDS[3])
            tableGrid[19] = true;

        if (shouts[0].GetVoice() != shouts[shoutCount - 1].GetVoice())
            tableGrid[20] = true;

        if ((femaleDirectionCount[0] > 0 && femaleDirectionCount[2] > 0) || (femaleDirectionCount[1] > 0 && femaleDirectionCount[3] > 0) ||
            (maleDirectionCount[0] > 0 && maleDirectionCount[2] > 0) || (maleDirectionCount[1] > 0 && maleDirectionCount[3] > 0) ||
            (childDirectionCount[0] > 0 && childDirectionCount[2] > 0) || (childDirectionCount[1] > 0 && childDirectionCount[3] > 0))
            tableGrid[21] = true;

        if (directionCount[0] > 0 && directionCount[1] > 0 && directionCount[2] > 0 && directionCount[3] > 0)
            tableGrid[22] = true;

        if (voiceCount[0] > 0 && voiceCount[1] > 0 && voiceCount[2] > 0)
            tableGrid[23] = true;

        if (shoutCount == 5)
            tableGrid[24] = true;


        return tableGrid;
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


    // Twitch Plays - made by eXish

    //tp command handler
    #pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} toggle/switch [Toggles the switch to turn the speaker on or off] | !{0} ldru [Presses the specified arrow button(s)]";
    #pragma warning restore 414
    IEnumerator ProcessTwitchCommand(string command) {
        if (Regex.IsMatch(command, @"^\s*toggle\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) || Regex.IsMatch(command, @"^\s*switch\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) {
            yield return null;
            Switch.OnInteract();
            yield break;
        }
        command = command.Replace(" ", "");
        char[] validmoves = { 'l', 'L', 'u', 'U', 'r', 'R', 'd', 'D' };
        for (int i = 0; i < command.Length; i++) {
            if (!validmoves.Contains(command.ElementAt(i))) {
                yield return null;
                yield return "sendtochaterror The specified arrow button '" + command.ElementAt(i) + "' is not valid!";
                yield break;
            }
        }
        yield return null;
        for (int i = 0; i < command.Length; i++) {
            if (command.ElementAt(i).Equals('l') || command.ElementAt(i).Equals('L'))
            {
                ArrowButtons[3].OnInteract();
            }
            else if (command.ElementAt(i).Equals('d') || command.ElementAt(i).Equals('D'))
            {
                ArrowButtons[2].OnInteract();
            }
            else if (command.ElementAt(i).Equals('r') || command.ElementAt(i).Equals('R'))
            {
                ArrowButtons[1].OnInteract();
            }
            else if (command.ElementAt(i).Equals('u') || command.ElementAt(i).Equals('U'))
            {
                ArrowButtons[0].OnInteract();
            }
            yield return new WaitForSeconds(0.1f);
        }
    }

    //tp force solve handler
    IEnumerator TwitchHandleForcedSolve() {
        while (!moduleSolved) {
            GetDestination();
            if (desiredDirection.Equals("Up")) {
                ArrowButtons[0].OnInteract();
            }
            else if (desiredDirection.Equals("Right")) {
                ArrowButtons[1].OnInteract();
            }
            else if (desiredDirection.Equals("Down")) {
                ArrowButtons[2].OnInteract();
            }
            else if (desiredDirection.Equals("Left")) {
                ArrowButtons[3].OnInteract();
            }
            yield return new WaitForSeconds(0.1f);
        }
    }
}