using UnityEngine;

[System.Serializable]
public class QuizQuestion
{
    [TextArea(2, 4)] public string question;

    [Tooltip("2–4 opciones")]
    public string[] options;

    [Tooltip("Índice (0-based) de la opción correcta")]
    public int correctIndex;

    [Tooltip("Justificación por opción (misma longitud que 'options'). " +
             "Para la correcta puede ir vacío o un refuerzo positivo.")]
    [TextArea(2, 3)]
    public string[] optionJustifications;
}

[CreateAssetMenu(fileName = "NewQuizData", menuName = "Quiz/QuizData")]
public class QuizData : ScriptableObject
{
    [Tooltip("Hasta 5 preguntas (o las que necesites)")]
    public QuizQuestion[] questions;
}
