using UnityEngine;

namespace Gibi.Pets
{
    /// <summary>
    /// P0 acceptance loop: fetch the visible toy, return and drop it, then rest visibly
    /// at the dog-house threshold. It starts only after a verified pet is bound.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SandboxDemoDirector : MonoBehaviour
    {
        private enum DemoPhase { WaitingToFetch, Fetching, WaitingToRest, SeekingRest, Resting }

        [SerializeField] private FetchToy toy;
        [SerializeField] private RestAffordance dogHouseRest;
        [SerializeField] private Transform fetchReturnPoint;
        [SerializeField] private bool loop = true;
        [SerializeField, Min(0f)] private float initialDelayS = 1.0f;
        [SerializeField, Min(0f)] private float betweenActionsS = 1.0f;
        [SerializeField, Min(0.5f)] private float restDurationS = 4.0f;

        private PetController _pet;
        private DemoPhase _phase;
        private float _timer;
        private int _fetchBaseline;

        public bool IsBound => _pet != null;

        public void Configure(FetchToy fetchToy, RestAffordance rest,
                              Transform returnPoint, bool shouldLoop = true)
        {
            toy = fetchToy;
            dogHouseRest = rest;
            fetchReturnPoint = returnPoint;
            loop = shouldLoop;
        }

        public void BindPet(PetController pet)
        {
            _pet = pet;
            toy?.ResetToHome();
            _phase = DemoPhase.WaitingToFetch;
            _timer = initialDelayS;
            _fetchBaseline = pet != null ? pet.CompletedFetches : 0;
        }

        public void UnbindPet()
        {
            _pet = null;
            toy?.ResetToHome();
            _phase = DemoPhase.WaitingToFetch;
            _timer = initialDelayS;
        }

        private void Update()
        {
            if (_pet == null || toy == null || dogHouseRest == null) return;

            _timer -= Time.deltaTime;
            switch (_phase)
            {
                case DemoPhase.WaitingToFetch:
                    if (_timer > 0f) return;
                    toy.ResetToHome();
                    _fetchBaseline = _pet.CompletedFetches;
                    Vector3 returnAt = fetchReturnPoint != null
                        ? fetchReturnPoint.position
                        : _pet.transform.position;
                    if (_pet.CueFetch(toy, returnAt))
                    {
                        _phase = DemoPhase.Fetching;
                        Debug.Log("[GibiWorld] demo: FETCH_STARTED");
                    }
                    break;

                case DemoPhase.Fetching:
                    if (_pet.CompletedFetches <= _fetchBaseline) return;
                    _phase = DemoPhase.WaitingToRest;
                    _timer = betweenActionsS;
                    Debug.Log("[GibiWorld] demo: FETCH_COMPLETED");
                    break;

                case DemoPhase.WaitingToRest:
                    if (_timer > 0f) return;
                    if (_pet.CueRest(dogHouseRest))
                    {
                        _phase = DemoPhase.SeekingRest;
                        Debug.Log("[GibiWorld] demo: REST_STARTED");
                    }
                    break;

                case DemoPhase.SeekingRest:
                    if (!_pet.IsEngaged) return;
                    _phase = DemoPhase.Resting;
                    _timer = restDurationS;
                    Debug.Log("[GibiWorld] demo: REST_ENGAGED");
                    break;

                case DemoPhase.Resting:
                    if (_timer > 0f) return;
                    if (!loop) return;
                    _pet.ExitAffordance();
                    _phase = DemoPhase.WaitingToFetch;
                    _timer = betweenActionsS;
                    break;
            }
        }
    }
}
