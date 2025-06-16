using UnityEngine;

[System.Serializable]
public class QuizQuestion
{
    [TextArea(2, 4)] public string question;
    [Tooltip("2–4 opciones")]
    public string[] options;
    [Tooltip("Índice (0-based) de la opción correcta")]
    public int correctIndex;
}

[CreateAssetMenu(fileName = "NewQuizData", menuName = "Quiz/QuizData")]
public class QuizData : ScriptableObject
{
    [Tooltip("Hasta 5 preguntas")]
    public QuizQuestion[] questions;
}
