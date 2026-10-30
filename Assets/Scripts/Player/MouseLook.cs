using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("Nézet beállítások")]
    // Az egér érzékenysége
    public float mouseSensitivity = 800f;
    
    // Hivatkozás a játékos fő testére (ezt fogjuk forgatni az X tengely mentén)
    public Transform playerBody;

    // A kamera fel-le forgásának belső értéke
    private float xRotation = 0f;

    void Start()
    {
        // Játék indulásakor az egeret középre zárjuk és eltüntetjük a képernyőről
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Egér bemenetek lekérése, szorozva az érzékenységgel és az idővel (függetlenítve a framerate-től)
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Az Y egérmozgás (fel-le) kivonása a belső forgási értékből (ha hozzáadnánk, fordítva működne a kamera)
        xRotation -= mouseY;
        
        // A forgás korlátozása -90 és 90 fok közé, hogy ne tudjuk "áthajtani" a fejet a hátunkba
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // A kamera fel-le forgatása a korlátozott érték alapján
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        
        // A teljes játékos karakter elforgatása jobbra-balra az X egérmozgás alapján
        playerBody.Rotate(Vector3.up * mouseX);
    }
}