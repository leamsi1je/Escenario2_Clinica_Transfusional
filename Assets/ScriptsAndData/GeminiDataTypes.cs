using System;

[Serializable]
public class GeminiRequest
{
    public Content[] contents;
}

[Serializable]
public class Content
{
    public string role;
    public Part[] parts;
}

[Serializable]
public class Part
{
    public string text;
}

[Serializable]
public class GeminiResponse
{
    public Candidate[] candidates;
}

[Serializable]
public class Candidate
{
    public Content content;
}

[Serializable]
public class TransformInstruction
{
    public float posX, posY, posZ;
    public float rotX, rotY, rotZ;
    public float scaleX, scaleY, scaleZ;

    public bool modifyPosition;
    public bool modifyRotation;
    public bool modifyScale;

    // NUEVO: Gemini nos explicará qué va a hacer
    public string explanation;
}