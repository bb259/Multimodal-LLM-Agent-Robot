using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterControl : MonoBehaviour
{
    // Start is called before the first frame update
    private CharacterControl _character;
    private Animator _animator;
    void Start()
    {
        _character = GetComponent<CharacterControl>();
        _animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        Vector3 dir=new Vector3(horizontal,0,vertical);
        if(dir != Vector3.zero)
       {
        transform.rotation=Quaternion.LookRotation(dir);
        _animator.SetBool("isWalk",true);
        transform.Translate(Vector3.forward*2*Time.deltaTime);
       }else
       {
        _animator.SetBool("isWalk",false);
       }
       if (Input.GetKeyDown (KeyCode.E))  
       {  
        Debug.Log("您按下了E键");  
       }
        
    }
}
