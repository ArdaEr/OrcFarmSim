using System;
using UnityEngine;

namespace OrcFarm.Player
{
    public class PlayerVisual : MonoBehaviour
    {
        [SerializeField] private MeshRenderer meshRenderer;
        private void Awake()
        {
            meshRenderer.enabled = false;
        }
    }
}
