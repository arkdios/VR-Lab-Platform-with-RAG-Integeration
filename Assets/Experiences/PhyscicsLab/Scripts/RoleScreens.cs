using System;
using UnityEngine;

namespace PhysicsLab.Lab
{
    public enum UserRole { Student, Instructor }

    /// <summary>
    /// Shows the role select screen, then the screen for the chosen role.
    /// Roles only change what the screen shows.
    /// This is not a login and gives no protection, therefore, anyone can press Instructor.
    /// </summary>
    public class RoleScreens : MonoBehaviour
    {
        [SerializeField] private GameObject roleSelectScreen;
        [SerializeField] private GameObject studentScreen;
        [SerializeField] private GameObject instructorScreen;

        public event Action<UserRole> RoleChosen;

        private void Start() => ShowOnly(roleSelectScreen);

        // These three are wired to Button On Click events in the Inspector.
        public void ChooseStudent() => Choose(UserRole.Student);

        public void ChooseInstructor() => Choose(UserRole.Instructor);

        public void ReturnToRoleSelect() => ShowOnly(roleSelectScreen);

        private void Choose(UserRole role)
        {
            ShowOnly(role == UserRole.Student ? studentScreen : instructorScreen);
            RoleChosen?.Invoke(role);
        }

        private void ShowOnly(GameObject screen)
        {
            roleSelectScreen.SetActive(screen == roleSelectScreen);
            studentScreen.SetActive(screen == studentScreen);
            instructorScreen.SetActive(screen == instructorScreen);
        }
    }
}