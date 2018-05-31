using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;
using KMHelper;

public class calcScript : MonoBehaviour {

    #region vars

    public KMAudio audio;
    public KMBombModule module;
    public KMBombInfo info;
    //public KMModSettings settings;
    public KMSelectable[] numButtons;
    public KMSelectable neg, chk, left, right;
    public TextMesh XDisplay, YDisplay, parADisplay, parBDisplay;
    public MeshRenderer ledC;
    public TextMesh[] labelsC, inputBtnsC;

    private static int _moduleIdCounter = 1;
    private int _moduleId = 0;
    private bool _isSolved = false, _lightsOn = false;

    public const int Y_VALUE_RANGE_MAX = 50, Y_VALUE_RANGE_MIN = -50;
    public const int X_LOW_VALUE_RANGE_MAX = 10, X_LOW_VALUE_RANGE_MIN = -10;
    public const int X_LOW_COUNT_RANGE_MIN = 7, X_LOW_COUNT_RANGE_MAX = 12;
    public const int X_HIGH_VALUE_ABS_RANGE_MIN = 11, X_HIGH_VALUE_ABS_RANGE_MAX = 50;
    public const int X_HIGH_COUNT_RANGE_MIN = 3, X_HIGH_COUNT_RANGE_MAX = 8;
    public const int MAX_CALCULATION_STEPS = 3;
    public const float X_HIGH_VALUE_NEG_CHANCE = 0.5f;
    public const float Y_VALUE_SYMBOLIC_CHANCE = 0.5f;

    public static readonly char[] greekAlphabet = {'α', 'β', 'γ', 'δ', 'ε', 'ζ', 'η', 'θ', 'ι', 'κ', 'λ', 'μ', 'ν', 'ξ', 'ο', 'π', 'ρ', 'σ', 'τ', 'υ', 'φ', 'χ', 'ψ', 'ω'};
    public static int[] greekVals = new int[24];
    public static readonly Color[] otherColors = { Color.magenta, new Color(0.8f, 0.5f, 0f), new Color(0.73f, 0.16f, 0.96f), new Color(0.584f, 0.271f, 0.208f), Color.white};
    private int minX, minY, maxX, maxY;
    private DataValue parA, parB;
    private DataValue[] XVals, YVals;
    private int opCode;

    private int curDataPntIndex = 0, curInput = 0;
    private bool negCurInput = false;

    #endregion

    private class DataValue
    {
        public int val;
        public bool symbolic;
        int symIndex, adder;
        string symDisp;

        public string Display
        {
            get
            {
                if (symbolic && symDisp != null) return symDisp;
                else return val.ToString();
            }
        }

        public DataValue(int value, bool sym)
        {
            symbolic = sym;
            val = value;
        }

        public void CreateSymbolicDisplay(bool parm)
        {
            if(symbolic)
            {
                symIndex = Random.Range(0, 24);
                while(parm && (symIndex == 10 || symIndex == 21)) symIndex = Random.Range(0, 24);
                adder = val - greekVals[symIndex];
                if (adder > 0)
                    symDisp = greekAlphabet[symIndex] + "+" + adder.ToString();
                else if (adder < 0)
                    symDisp = greekAlphabet[symIndex] + "-" + (-adder).ToString();
                else
                    symDisp = greekAlphabet[symIndex].ToString();
            }
        }

        public string LogString()
        {
            if (!symbolic || symDisp == null) return val.ToString();
            else return "{symbolic: " + symDisp + ", value: " + val + "}";
        }
    }

    #region initialization

    //loading
    void Start () {
        _moduleId = _moduleIdCounter++;
        module.OnActivate += Activate;
        initPuzzleNumeric();
    }

    //light off (button interactions and init displays)
    public void Awake()
    {
        setTextColors(true);
        left.OnInteract += delegate ()
        {
            switchDisplay(false);
            return false;
        };
        right.OnInteract += delegate ()
        {
            switchDisplay(true);
            return false;
        };
        for(int i = 0; i < 10; i++)
        {
            int j = i;
            numButtons[i].OnInteract += delegate ()
            {
                numBtnPress(j);
                return false;
            };
        }
        neg.OnInteract += delegate ()
        {
            negBtnPress();
            return false;
        };
        chk.OnInteract += delegate ()
        {
            submitBtnPress();
            return false;
        };
    }

    //light on (begin allowing interactions)
    void Activate()
    {
        initVals();
        initPuzzleSymbolic();
        initDisplays();
        _lightsOn = true;
        setTextColors(false);
        module.GetComponent<KMGameInfo>().OnLightsChange += OnLightChange;
        Debug.LogFormat("[Greek Calculus #{0}] Module activated.", _moduleId);
    }

    void OnLightChange(bool isOn)
    {
        setTextColors(!isOn);
    }

    void setTextColors(bool hidden)
    {
        if(hidden)
        {
            parADisplay.color = Color.black;
            parBDisplay.color = Color.black;
            foreach (TextMesh btn in inputBtnsC)
                btn.color = Color.black;
            foreach (TextMesh tm in labelsC)
                tm.color = Color.black;
        }
        else
        {
            parADisplay.color = Color.blue;
            parBDisplay.color = Color.yellow;
            foreach (TextMesh btn in inputBtnsC)
                btn.color = Color.white;
            foreach (TextMesh tm in labelsC)
                tm.color = Color.black;
        }
    }

    void initPuzzleNumeric()
    {
        SortedList tempVals = new SortedList();
        //settings.Settings = "";
        //randomize low-X data points
        List<int> lowVals = new List<int>();
        for (int i = X_LOW_VALUE_RANGE_MIN; i <= X_LOW_VALUE_RANGE_MAX; i++) lowVals.Add(i);

        int lowDataPointCnt = Random.Range(X_LOW_COUNT_RANGE_MIN, X_LOW_COUNT_RANGE_MAX + 1);
        Debug.LogFormat("[Greek Calculus #{0}] Module will have {1} low data points.", _moduleId, lowDataPointCnt);
        for(int i = 0; i < lowDataPointCnt; i++)
        {
            int rand = Random.Range(0, lowVals.Count);
            int newX = lowVals[rand];
            int newY = Random.Range(Y_VALUE_RANGE_MIN, Y_VALUE_RANGE_MAX + 1);
            tempVals.Add(newX, newY);
            lowVals.RemoveAt(rand);
            Debug.LogFormat("[Greek Calculus #{0}] Low data point generated: ({1}, {2}).", _moduleId, newX, newY);
        }

        //randomize high-X data points
        int highDataPointCnt = Random.Range(X_HIGH_COUNT_RANGE_MIN, X_HIGH_COUNT_RANGE_MAX + 1);
        Debug.LogFormat("[Greek Calculus #{0}] Module will have {1} high data points.", _moduleId, highDataPointCnt);
        for (int i = 0; i < highDataPointCnt; i++)
        {
            while (true)
            {
                int rand = Random.Range(X_HIGH_VALUE_ABS_RANGE_MIN, X_HIGH_VALUE_ABS_RANGE_MAX + 1);
                if (Random.value < X_HIGH_VALUE_NEG_CHANCE) rand = -rand;
                if(!tempVals.Contains(rand))
                {
                    int newX = rand;
                    int newY = Random.Range(Y_VALUE_RANGE_MIN, Y_VALUE_RANGE_MAX + 1);
                    tempVals.Add(newX, newY);
                    Debug.LogFormat("[Greek Calculus #{0}] High data point generated: ({1}, {2}).", _moduleId, newX, newY);
                    break;
                }
            }
        }

        //initialize data points
        maxX = int.MinValue;
        maxY = int.MinValue;
        minX = int.MaxValue;
        minY = int.MaxValue;
        XVals = new DataValue[tempVals.Count];
        YVals = new DataValue[tempVals.Count];
        for ( int i = 0; i < tempVals.Count; i++)
        {
            bool sym = (Random.value < Y_VALUE_SYMBOLIC_CHANCE);
            XVals[i] = new DataValue((int)tempVals.GetKey(i), false);
            YVals[i] = new DataValue((int)tempVals.GetByIndex(i), sym);
            if (XVals[i].val < minX) minX = XVals[i].val;
            if (XVals[i].val > maxX) maxX = XVals[i].val;
            if (!sym && YVals[i].val < minY) minY = YVals[i].val;
            if (!sym && YVals[i].val > maxY) maxY = YVals[i].val;
            Debug.LogFormat("[Greek Calculus #{0}] Data point: ({1}, {2}) added to list, symbolic = {3}", _moduleId, XVals[i].val, YVals[i].val, YVals[i].symbolic);
        }
        //very rare: when all Y values are symbolic, force one non-symbolic y value
        if(minY == int.MaxValue)
        {
            int numInd = Random.Range(0, XVals.Length);
            YVals[numInd] = new DataValue(YVals[numInd].val, false);
            minY = YVals[numInd].val;
            maxY = YVals[numInd].val;
            Debug.LogFormat("[Greek Calculus #{0}] No data point is symbolic! Data point: ({1}, {2}) is changed to be symbolic.", _moduleId, XVals[numInd].val, YVals[numInd].val);
        }
        Debug.LogFormat("[Greek Calculus #{0}] Found min X: {1}, max X: {2}", _moduleId, minX, maxX);
        Debug.LogFormat("[Greek Calculus #{0}] Found min numeric Y: {1}, max numeric Y: {2}", _moduleId, minY, maxY);

        opCode = Random.Range(0, 5);
        switch (opCode)
        {
            case 0:
                {
                    ledC.material.color = Color.green;
                    Debug.LogFormat("[Greek Calculus #{0}] Generated LED color: green (operation = deriv)", _moduleId);
                    break;
                }
            case 1:
                {
                    ledC.material.color = Color.red;
                    Debug.LogFormat("[Greek Calculus #{0}] Generated LED color: red (operation = integ left-end)", _moduleId);
                    break;
                }
            case 2:
                {
                    ledC.material.color = Color.blue;
                    Debug.LogFormat("[Greek Calculus #{0}] Generated LED color: blue (operation = integ right-end)", _moduleId);
                    break;
                }
            case 3:
                {
                    ledC.material.color = Color.yellow;
                    Debug.LogFormat("[Greek Calculus #{0}] Generated LED color: yellow (operation = integ average)", _moduleId);
                    break;
                }
            case 4:
                {
                    ledC.material.color = otherColors[Random.Range(0, otherColors.Length)];
                    Debug.LogFormat("[Greek Calculus #{0}] Generated LED color: other (operation = sum)", _moduleId);
                    break;
                }
        }

        //randomize parameters
        int parAIndex = Random.Range(0, XVals.Length);
        parA = new DataValue(XVals[parAIndex].val, true);
        Debug.LogFormat("[Greek Calculus #{0}] Generated parameter A: {1}", _moduleId, parA.val);

        //avoids picking double min/max, which leads to invalid differential estimation
        if (opCode == 0) parB = new DataValue(XVals[Random.Range(1, XVals.Length - 1)].val, true);
        //generate other parameter to be within range of MAX_CALCULATION_STEPS
        else
        {
            int tmpMin = Mathf.Max(0, parAIndex - MAX_CALCULATION_STEPS);
            int tmpMax = Mathf.Min(XVals.Length, parAIndex + MAX_CALCULATION_STEPS + 1);
            parB = new DataValue(XVals[Random.Range(tmpMin, tmpMax)].val, true);
        }
        Debug.LogFormat("[Greek Calculus #{0}] Generated parameter B: {1}", _moduleId, parB.val);
    }

    void initVals()
    {
        greekVals[0] = info.GetOnIndicators().Count();
        greekVals[1] = info.GetBatteryCount(KMBombInfoExtensions.KnownBatteryType.AA);
        greekVals[2] = info.GetPortCount();
        greekVals[3] = info.GetSerialNumberNumbers().Last();
        greekVals[4] = maxY; //max of y vals
        greekVals[5] = XVals.Length; //# of data points
        greekVals[6] = minX; //min of x vals
        greekVals[7] = minY; //min of y vals
        greekVals[8] = info.GetOffIndicators().Count();
        greekVals[9] = info.GetSerialNumberLetters().Count() * info.GetSerialNumberNumbers().Count();
        greekVals[10] = Mathf.Abs(parA.val - parB.val);//diff between parameters
        greekVals[11] = info.GetBatteryHolderCount();
        greekVals[12] = info.GetBatteryCount(KMBombInfoExtensions.KnownBatteryType.D);
        greekVals[13] = maxX; //max of x vals
        greekVals[14] = info.GetPortPlateCount();
        greekVals[15] = 3;
        greekVals[16] = info.GetPorts().Distinct().Count();
        greekVals[17] = info.GetSerialNumberNumbers().Sum();
        greekVals[18] = 6;
        greekVals[19] = info.GetBatteryCount();
        greekVals[20] = 2;
        greekVals[21] = parA.val + parB.val; //sum of parameters
        greekVals[22] = info.GetIndicators().Count();
        greekVals[23] = info.GetSerialNumberNumbers().First();
    }

    void initPuzzleSymbolic()
    {
        for(int i = 0; i < YVals.Length; i++)
        {
            if (YVals[i].symbolic)
            {
                YVals[i].CreateSymbolicDisplay(false);
                Debug.LogFormat("[Greek Calculus #{0}] Generated symbolic form for y-val of x = {1}: {2}", _moduleId, XVals[i].val, YVals[i].LogString());
            }
        }
        parA.CreateSymbolicDisplay(true);
        Debug.LogFormat("[Greek Calculus #{0}] Generated symbolic form for parameter A: {1}", _moduleId, parA.LogString());
        parB.CreateSymbolicDisplay(true);
        Debug.LogFormat("[Greek Calculus #{0}] Generated symbolic form for parameter B: {1}", _moduleId, parB.LogString());
    }

    void initDisplays()
    {
        parADisplay.text = parA.Display;
        parBDisplay.text = parB.Display;
        XDisplay.text = XVals[0].Display;
        YDisplay.text = YVals[0].Display;
        curDataPntIndex = 0;
    }

    #endregion

    #region Button Handling

    void switchDisplay(bool r)
    {
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, (r ? right : left).transform);
        (r ? right : left).AddInteractionPunch();
        if (!_lightsOn || _isSolved) return;

        if (r) curDataPntIndex = (curDataPntIndex + 1) % XVals.Length;
        else curDataPntIndex = (curDataPntIndex + XVals.Length - 1) % XVals.Length;
        XDisplay.text = XVals[curDataPntIndex].Display;
        YDisplay.text = YVals[curDataPntIndex].Display;
    }

    void numBtnPress(int num)
    {
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, numButtons[num].transform);
        numButtons[num].AddInteractionPunch();
        if (!_lightsOn || _isSolved) return;

        if(curInput >= 100000000)//strike
        {
            Debug.LogFormat("[Greek Calculus #{0}] STRIKE! Digit {1} pushed, causing the user answer to go over nine digits. Input has been reset.", _moduleId, num);
            module.HandleStrike();
            curInput = 0;
            negCurInput = false;
            return;
        }
        curInput = curInput * 10 + num;
        Debug.LogFormat("[Greek Calculus #{0}] Digit {1} pushed! Current user answer: {2}.", _moduleId, num, (negCurInput ? -curInput : curInput));
    }

    void negBtnPress()
    {
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, neg.transform);
        neg.AddInteractionPunch();
        if (!_lightsOn || _isSolved) return;

        negCurInput = !negCurInput;
        Debug.LogFormat("[Greek Calculus #{0}] Negate button pushed! Current user answer: {1}.", _moduleId, (negCurInput ? -curInput : curInput));
    }

    void submitBtnPress()
    {
        audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, chk.transform);
        chk.AddInteractionPunch();
        if (!_lightsOn || _isSolved) return;

        Debug.LogFormat("[Greek Calculus #{0}] Submit button pushed! Checking answer...", _moduleId);
        int correctAnswer = getAnswer();
        int playerAnswer = (negCurInput ? -curInput : curInput);
        Debug.LogFormat("[Greek Calculus #{0}] Correct answer is {1}, player answer is {2}.", _moduleId, correctAnswer, playerAnswer);
        if (correctAnswer == playerAnswer)
        {
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, module.transform);
            Debug.LogFormat("[Greek Calculus #{0}] PASSED!", _moduleId);
            module.HandlePass();
            _isSolved = true;
        }
        else
        {
            Debug.LogFormat("[Greek Calculus #{0}] STRIKE! Answer is incorrect. Input has been reset.", _moduleId);
            module.HandleStrike();
            curInput = 0;
            negCurInput = false;
        }
    }

    #endregion

    #region Answer Calculations

    int getAnswer()
    {
        switch(opCode)
        {
            case 0:
                {
                    return Derivative((float)(parA.val + parB.val) / 2);
                }
            case 1:
            case 2:
            case 3:
                {
                    return Integral(parA.val, parB.val, opCode);
                }
            case 4:
                {
                    return Sum(parA.val, parB.val);
                }
            default:
                {
                    Debug.LogFormat("[Greek Calculus #{0}] Error finding answer. Unexpected opcode: {1}", _moduleId, opCode);
                    return 0;
                }
        }
    }
/*
    int LRAM(int lowLim, int highLim)
    {
        Debug.LogFormat("[Greek Calculus #{0}] LRAM: Start integration for X {1}->{2}", _moduleId, lowLim, highLim);
        int sum = 0;
        bool negResult = false;
        if (lowLim == highLim) return 0;
        else if(lowLim > highLim)
        {
            negResult = true;
            int tmp = highLim;
            highLim = lowLim;
            lowLim = tmp;
            Debug.LogFormat("[Greek Calculus #{0}] LRAM: Lower limit larger than higher limit. Negate answer and flip limits.", _moduleId);
        }

        //find indices
        int lowLimInd = -1, highLimInd = -1;
        for(int i = 0; i < XVals.Length; i++)
        {
            if (XVals[i].val == lowLim) lowLimInd = i;
            else if(XVals[i].val == highLim)
            {
                highLimInd = i;
                break;
            }
        }

        for(int i = lowLimInd; i < highLimInd; i++)
        {
            sum += (XVals[i + 1].val - XVals[i].val) * YVals[i].val;
            Debug.LogFormat("[Greek Calculus #{0}] LRAM: Step: X {1}->{2}, Y={3}, Step product={4}, Current sum={5}", _moduleId, XVals[i].val, XVals[i+1].val, YVals[i].Display, (XVals[i + 1].val - XVals[i].val) * YVals[i].val, sum);
        }
        Debug.LogFormat("[Greek Calculus #{0}] LRAM: Final answer: {1}", _moduleId, negResult ? -sum : sum);
        return negResult ? -sum : sum;
    }

    int RRAM(int lowLim, int highLim)
    {
        Debug.LogFormat("[Greek Calculus #{0}] RRAM: Start integration for X {1}->{2}", _moduleId, lowLim, highLim);
        int sum = 0;
        bool negResult = false;
        if (lowLim == highLim) return 0;
        else if (lowLim > highLim)
        {
            negResult = true;
            int tmp = highLim;
            highLim = lowLim;
            lowLim = tmp;
            Debug.LogFormat("[Greek Calculus #{0}] RRAM: Lower limit larger than higher limit. Negate answer and flip limits.", _moduleId);
        }

        //find indices
        int lowLimInd = -1, highLimInd = -1;
        for (int i = 0; i < XVals.Length; i++)
        {
            if (XVals[i].val == lowLim) lowLimInd = i;
            else if (XVals[i].val == highLim)
            {
                highLimInd = i;
                break;
            }
        }

        for (int i = lowLimInd; i < highLimInd; i++)
        {
            sum += (XVals[i + 1].val - XVals[i].val) * YVals[i+1].val;
            Debug.LogFormat("[Greek Calculus #{0}] RRAM: Step: X {1}->{2}, Y={3}, Step product={4}, Current sum={5}", _moduleId, XVals[i].val, XVals[i + 1].val, YVals[i+1].Display, (XVals[i + 1].val - XVals[i].val) * YVals[i+1].val, sum);
        }
        Debug.LogFormat("[Greek Calculus #{0}] RRAM: Final answer: {1}", _moduleId, negResult ? -sum : sum);
        return negResult ? -sum : sum;
    }

    int Trapezoidal(int lowLimInd, int highLimInd)
    {
        Debug.LogFormat("[Greek Calculus #{0}] Trape: Find LRAM", _moduleId);
        int a = LRAM(lowLimInd, highLimInd);
        Debug.LogFormat("[Greek Calculus #{0}] Trape: LRAM result: {1}", _moduleId, a);
        Debug.LogFormat("[Greek Calculus #{0}] Trape: Find RRAM", _moduleId);
        int b = RRAM(lowLimInd, highLimInd);
        Debug.LogFormat("[Greek Calculus #{0}] Trape: RRAM result: {1}", _moduleId, b);
        Debug.LogFormat("[Greek Calculus #{0}] Trape: Answer is the average: {1}->round to {2}", _moduleId, (float)(a + b) / 2, Round((float)(a + b) / 2));
        return Round((float)(a + b) / 2);
    }
*/
    int Integral(int lowLim, int highLim, int opC)
    {
        Debug.LogFormat("[Greek Calculus #{0}] Integ: Start integration for X {1}->{2}", _moduleId, lowLim, highLim);
        float sum = 0f;
        bool negResult = false;
        if (lowLim == highLim)
        {
            Debug.LogFormat("[Greek Calculus #{0}] Integ: Limits are the same, answer is 0.", _moduleId);
            return 0;
        }
        else if (lowLim > highLim)
        {
            negResult = true;
            int tmp = highLim;
            highLim = lowLim;
            lowLim = tmp;
            Debug.LogFormat("[Greek Calculus #{0}] Integ: Lower limit larger than higher limit. Negate answer and flip limits.", _moduleId);
        }

        //find indices
        int lowLimInd = -1, highLimInd = -1;
        for (int i = 0; i < XVals.Length; i++)
        {
            if (XVals[i].val == lowLim) lowLimInd = i;
            else if (XVals[i].val == highLim)
            {
                highLimInd = i;
                break;
            }
        }

        for (int i = lowLimInd; i < highLimInd; i++)
        {
            if (opC == 1)
            {
                sum += (XVals[i + 1].val - XVals[i].val) * YVals[i].val;
                Debug.LogFormat("[Greek Calculus #{0}] Integ: Step: X {1}->{2}, Multiplier={3}(Left-end Y value), Step product={4}, Current sum={5}", _moduleId, XVals[i].val, XVals[i + 1].val, YVals[i].LogString(), (XVals[i + 1].val - XVals[i].val) * YVals[i].val, sum);
            }
            else if (opC == 2)
            {
                sum += (XVals[i + 1].val - XVals[i].val) * YVals[i + 1].val;
                Debug.LogFormat("[Greek Calculus #{0}] Integ: Step: X {1}->{2}, Multiplier={3} (Right-end Y value), Step product={4}, Current sum={5}", _moduleId, XVals[i].val, XVals[i + 1].val, YVals[i + 1].LogString(), (XVals[i + 1].val - XVals[i].val) * YVals[i+1].val, sum);
            }
            else
            {
                sum += (XVals[i + 1].val - XVals[i].val) * (YVals[i].val + YVals[i + 1].val) / 2.0f;
                Debug.LogFormat("[Greek Calculus #{0}] Integ: Step: X {1}->{2}, Multiplier=({3}+{4})/2={5} (average Y value), Step product={6}, Current sum={7}", _moduleId, XVals[i].val, XVals[i + 1].val, YVals[i].LogString(), YVals[i + 1].LogString(), (YVals[i].val + YVals[i + 1].val) / 2.0f, (XVals[i + 1].val - XVals[i].val) * ((YVals[i].val + YVals[i + 1].val) / 2.0f), sum);
            }
        }
        Debug.LogFormat("[Greek Calculus #{0}] Integ: Final answer: {1}->round to {2}", _moduleId, negResult ? -sum : sum, Round(negResult ? -sum : sum));
        return Round(negResult ? -sum : sum);
    }

    int Derivative(float x)
    {
        Debug.LogFormat("[Greek Calculus #{0}] Deriv: Average of parameters is {1}", _moduleId, x);
        int aInd = -1, bInd = -1;
        for(int i = 0; i < XVals.Length; i++)
        {
            aInd = bInd;
            bInd = i;
            if(XVals[i].val == x)
            {
                aInd = i - 1;
                bInd = i + 1;
                Debug.LogFormat("[Greek Calculus #{0}] Deriv: Data point is given! Ignore and find neighboring data points: ({1}, {2}) and ({3}, {4})", _moduleId, XVals[aInd].val, YVals[aInd].LogString(), XVals[bInd].val, YVals[bInd].LogString());
                break;
            }
            if(XVals[i].val > x)
            {
                Debug.LogFormat("[Greek Calculus #{0}] Deriv: Closest given data points are: ({1}, {2}) and ({3}, {4})", _moduleId, XVals[aInd].val, YVals[aInd].LogString(), XVals[bInd].val, YVals[bInd].LogString());
                break;
            }
        }
        float ans = ((float)YVals[bInd].val - YVals[aInd].val) / ((float)XVals[bInd].val - XVals[aInd].val);
        Debug.LogFormat("[Greek Calculus #{0}] Deriv: Calculate answer: ({4}-{2})/({3}-{1})={5}->round to {6}", _moduleId, XVals[aInd].val, YVals[aInd].val, XVals[bInd].val, YVals[bInd].val, ans, Round(ans));
        return Round(ans);
    }

    int Sum(int lowLim, int highLim)
    {
        Debug.LogFormat("[Greek Calculus #{0}] Sum: Start summation for X {1}->{2}", _moduleId, lowLim, highLim);
        int sum = 0;

        if (lowLim > highLim)
        {
            int tmp = highLim;
            highLim = lowLim;
            lowLim = tmp;
        }

        //find indices
        int lowLimInd = -1, highLimInd = -1;
        for (int i = 0; i < XVals.Length; i++)
        {
            if (XVals[i].val == lowLim) lowLimInd = i;
            if (XVals[i].val == highLim)
            {
                highLimInd = i;
                break;
            }
        }

        for (int i = lowLimInd; i <= highLimInd; i++)
        {
            sum += YVals[i].val;
            Debug.LogFormat("[Greek Calculus #{0}] Sum: Step: Valid point ({1}, {2}), Add {3}, Current sum={4}", _moduleId, XVals[i].val, YVals[i].LogString(), YVals[i].val, sum);
        }
        Debug.LogFormat("[Greek Calculus #{0}] Sum: Final answer: {1}", _moduleId, sum);
        return sum;
    }

    int Round(float x)
    {
        if (Mathf.Ceil(x) - x > x - Mathf.Floor(x)) return Mathf.FloorToInt(x);
        else return Mathf.CeilToInt(x);
    }

    #endregion
}
