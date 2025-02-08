using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parallax.Debugging
{
    using UnityEngine;

    // Cheers FreyaHolmer for this script. Only used in main menu
    // https://gist.github.com/FreyaHolmer/650ecd551562352120445513efa1d952
    public class FlyCamera : MonoBehaviour
    {
        public float acceleration = 20; // how fast you accelerate
        public float accSprintMultiplier = 80; // how much faster you go when "sprinting"
        public float lookSensitivity = 1; // mouse look sensitivity
        public float dampingCoefficient = 5; // how quickly you break to a halt after you stop your input
        public bool focusOnEnable = true; // whether or not to focus and lock cursor immediately on enable

        Vector3 velocity; // current velocity

        static bool Focused
        {
            get => Cursor.lockState == CursorLockMode.Locked;
            set
            {
                Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = value == false;
            }
        }

        void OnEnable()
        {
            if (focusOnEnable) Focused = true;
        }

        void OnDisable() => Focused = false;

        void Update()
        {
            // Input
            if (Focused)
                UpdateInput();
            else if (Input.GetMouseButtonDown(0))
                Focused = true;

            // Physics
            velocity = Vector3.Lerp(velocity, Vector3.zero, dampingCoefficient * Time.deltaTime);
            transform.position += velocity * Time.deltaTime;
        }

        void UpdateInput()
        {
            // Position
            velocity += GetAccelerationVector() * Time.deltaTime;

            // Rotation
            Vector2 mouseDelta = lookSensitivity * new Vector2(Input.GetAxis("Mouse X"), -Input.GetAxis("Mouse Y"));
            Quaternion rotation = transform.rotation;
            Quaternion horiz = Quaternion.AngleAxis(mouseDelta.x, Vector3.up);
            Quaternion vert = Quaternion.AngleAxis(mouseDelta.y, Vector3.right);
            transform.rotation = horiz * rotation * vert;

            // Leave cursor lock
            if (Input.GetKeyDown(KeyCode.Escape))
                Focused = false;
        }

        Vector3 GetAccelerationVector()
        {
            Vector3 moveInput = default;

            void AddMovement(KeyCode key, Vector3 dir)
            {
                if (Input.GetKey(key))
                    moveInput += dir;
            }

            AddMovement(KeyCode.W, Vector3.forward);
            AddMovement(KeyCode.S, Vector3.back);
            AddMovement(KeyCode.D, Vector3.right);
            AddMovement(KeyCode.A, Vector3.left);
            AddMovement(KeyCode.Space, Vector3.up);
            AddMovement(KeyCode.LeftControl, Vector3.down);
            Vector3 direction = transform.TransformVector(moveInput.normalized);

            if (Input.GetKey(KeyCode.LeftShift))
                return direction * (acceleration * accSprintMultiplier); // "sprinting"
            return direction * acceleration; // "walking"
        }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class MainMenuCamera : MonoBehaviour
    {
        bool movementEnabled = false;
        bool componentAdded = false;
        void Update()
        {
            if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKeyDown(KeyCode.M))
            {
                movementEnabled = !movementEnabled;
            }
            if (movementEnabled)
            {
                Debug.Log("Camera position: " + Camera.main.transform.position);
                
                if (!componentAdded)
                {
                    componentAdded = true;
                    Camera.main.gameObject.AddComponent<FlyCamera>();
                    MainMenuEnvLogic component = GameObject.FindObjectsOfType<MainMenuEnvLogic>().FirstOrDefault();
                    component.enabled = false;
                }
            }
            else
            {
                if (componentAdded)
                {
                    componentAdded = false;
                    Destroy(Camera.main.gameObject.GetComponent<FlyCamera>());

                    MainMenuEnvLogic component = GameObject.FindObjectsOfType<MainMenuEnvLogic>().FirstOrDefault();
                    component.enabled = true;
                }
            }
        }
    }
}
