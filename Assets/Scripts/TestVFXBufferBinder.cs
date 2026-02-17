using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// Auto-generated helper that creates a GraphicsBuffer and binds it to a VFX Graph property.
/// Attach this to the same GameObject as a VisualEffect component.
/// </summary>
[RequireComponent(typeof(VisualEffect))]
public class TestVFXBufferBinder : MonoBehaviour
{
    [Header("Buffer Config")]
    public string propertyName = "PointBuffer";
    public int bufferCount = 512;
    public int bufferStride = 12;

    private GraphicsBuffer _buffer;
    private VisualEffect _vfx;

    void OnEnable()
    {
        _vfx = GetComponent<VisualEffect>();
        _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferCount, bufferStride);

        // Initialize with zeros
        var data = new byte[bufferCount * bufferStride];
        _buffer.SetData(data);

        _vfx.SetGraphicsBuffer(propertyName, _buffer);
    }

    void OnDisable()
    {
        _buffer?.Release();
        _buffer = null;
    }

    /// <summary>
    /// Call from external scripts to update buffer data.
    /// </summary>
    public void SetData<T>(T[] data) where T : struct
    {
        if (_buffer != null)
        {
            _buffer.SetData(data);
            _vfx.SetGraphicsBuffer(propertyName, _buffer);
        }
    }

    /// <summary>
    /// Resize the buffer at runtime.
    /// </summary>
    public void Resize(int newCount)
    {
        _buffer?.Release();
        bufferCount = newCount;
        _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, bufferCount, bufferStride);
        _vfx.SetGraphicsBuffer(propertyName, _buffer);
    }
}
