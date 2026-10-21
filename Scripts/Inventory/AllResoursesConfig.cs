using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "AllResoursesConfig", menuName = "Resourses/AllResoursesConfig")]
public class AllResoursesConfig : ScriptableObject
{
    public IReadOnlyList<ResourseConfig> Resourses => _resourses;
    [SerializeField] private List<ResourseConfig> _resourses;
}
