using UnityEngine;
using UnityEngine.UI;

public class ProceduralTerrainGeneratorUI : MonoBehaviour
{
    private ProceduralTerrainGenerator generator;

    [Header("Sliders")]
    [SerializeField] private Slider baseFrequencySlider;
    [SerializeField] private Slider octavesSlider;
    [SerializeField] private Slider persistenceSlider;
    [SerializeField] private Slider lacunaritySlider;
    [SerializeField] private Slider noiseScaleSlider;
    [SerializeField] private Slider voronoiSlider;
    [SerializeField] private Slider temperatureSlider;

    [SerializeField] private Button generateButton;

    private void Awake() => Initialization();

    #region Initialization 

    private void Initialization()
    {
        generator = GetComponent<ProceduralTerrainGenerator>();

        baseFrequencySlider.value = generator.baseFrequency;
        octavesSlider.value = generator.octaves;
        persistenceSlider.value = generator.persistence;
        lacunaritySlider.value = generator.lacunarity;
        noiseScaleSlider.value = generator.noiseScale;
        voronoiSlider.value = generator.voronoiPointCount;
        temperatureSlider.value = generator.baseTemperature;

        generateButton.onClick.AddListener(ApplyAndGenerate);
    }

    #endregion

    #region Apply And Generate 

    void ApplyAndGenerate()
    {
        generator.baseFrequency = baseFrequencySlider.value;
        generator.octaves = (int)octavesSlider.value;
        generator.persistence = persistenceSlider.value;
        generator.lacunarity = lacunaritySlider.value;
        generator.noiseScale = noiseScaleSlider.value;
        generator.voronoiPointCount = (int)voronoiSlider.value;
        generator.baseTemperature = temperatureSlider.value;

        generator.GenerateTerrain();
    }

    #endregion
}
