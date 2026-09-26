using System;
using System.Collections.Generic;
using UnityEngine;
public enum SyncUpdateMode
{
    Update,
    FixedUpdate,
    LateUpdate
}
public class NetTransformHybrid : NetBehaviour
{
    [Header("Transform Common")]
    public Transform target;

    [Header("Interpolation")]
    public bool interpolatePosition = true;
    public bool interpolateRotation = true;
    public bool interpolateScale = false;

    [Header("Sync Setting")]
    public bool onlySyncOnChange = true;
    public bool syncPosition = true; 
    public bool syncRotation = true; 
    public bool syncScale = false;
    public float serverSnapshotOffset = 0.5f;
    public SyncUpdateMode updateMode = SyncUpdateMode.Update;

    [Header("Precision")]
    public float rotationPrecision = 0.01f;
    public float positionPrecision = 0.01f; // 1 cm
    public float scalePrecision = 0.01f; // 1 cm

    public bool isClientWithAuthority => isClient && authority;

    //mirror 这里server和client各有一份，可能是为了host模式下准备的
    protected readonly SortedList<double, TransformSnapshot> snapshots = new(16);

    private TransformSnapshot? pendingSnapshot = null;

    public uint sendIntervalMultiplier
    {
        get
        {
            if (syncInterval > 0)
            {

                float multiples = syncInterval / NetServer.sendInterval;
                uint res;
                res = Math.Max(1, (uint)Mathf.RoundToInt(multiples));
                return res;
            }

            return 1;
        }
    }
    //如果只使用远程时间戳作为快照序列的排序编号，
    //那么syncInterval大于NetServer.sendInterval，这会导致在update的时候会存在部分时间插值种只有一个值
    //同时在新的插值到达的时候，插值系数t往往不是从0->1而是从x->1，这样会产生卡顿和瞬移的现象
    //如果syncInterval小于NetServer.sendInterval则不会出现这种问题
    protected double timeStampOffset => NetServer.sendInterval * (sendIntervalMultiplier - 1);

    //base
    public void Reset()
    {
        ResetState();
    }
    public override void ResetState()
    {
        if (target == null)
            target = GetComponent<Transform>();

        snapshots.Clear();
        Physics.SyncTransforms();

        lastBaselineTransform = new TransformSnapshot
        {
            remoteTime = 0,
            localTime = 0,
            position = GetPosition(),
            rotation = GetRotation(),
            scale = GetScale()
        };
    }
    protected Vector3 GetPosition() => target.position;
    protected Vector3 GetScale() => target.localScale;
    protected Quaternion GetRotation() => target.rotation;

    protected void SetPosition(Vector3 position) => target.position = position;   
    protected void SetRotation(Quaternion rotation) => target.rotation = rotation;
    protected void SetScale(Vector3 scale) => target.localScale = scale;

    protected void Apply(TransformSnapshot snapshot0, TransformSnapshot? snapshot1 = null, float t = 0)
    {
        TransformSnapshot interpolatedSnapshot = default;

        if (snapshot1 != null)
            interpolatedSnapshot = TransformSnapshot.Interpolate(snapshot0, snapshot1.Value, t);
        
        if (interpolatePosition && snapshot1 != null)
            SetPosition(interpolatedSnapshot.position);
        else
            SetPosition(snapshot0.position);

        if (interpolateRotation && snapshot1 != null)
            SetRotation(interpolatedSnapshot.rotation);
        else
            SetRotation(snapshot0.rotation);

        if (interpolateScale && snapshot1 != null)
            SetScale(interpolatedSnapshot.scale);
        else
            SetScale(snapshot0.scale);
    }

    protected void AddSnapshot(
        SortedList<double, TransformSnapshot> snapshots, 
        double timeStamp,
        int limit,
        Vector3? position, 
        Quaternion? rotation, 
        Vector3? scale)
    {
        Vector3 pos = position.HasValue? position.Value : GetPosition();
        Quaternion rot = rotation.HasValue ? rotation.Value : GetRotation();
        Vector3 sca = scale.HasValue ? position.Value : GetScale();

        TransformSnapshot snapshot = new TransformSnapshot()
        {
            remoteTime = timeStamp,
            localTime = NetTime.localTime,
            position = pos,
            rotation = rot,
            scale = sca
        };
        SnapshotInterpolation.InsertIfNotExists(snapshots, limit, snapshot);
    }
    protected TransformSnapshot CurrentTransformSnapshot()
    {
        return new TransformSnapshot()
        {
            localTime = NetTime.localTime,
            remoteTime = 0,
            position = GetPosition(),
            rotation = GetRotation(),
            scale = GetScale()
        };
    }
    #region Common

    public void Awake()
    {
        if (target == null)
            target = transform;

        Reset();
    }
    private void Update()
    {
        if (updateMode == SyncUpdateMode.Update)
            UpdateTransform();
    }
    private void FixedUpdate()
    {
        if (updateMode == SyncUpdateMode.FixedUpdate)
            UpdateTransform();

        if(pendingSnapshot.HasValue)
        {
            Apply(pendingSnapshot.Value);
            pendingSnapshot = null;
        }

        //服务端不应该将客户端权威的数据同步给对应客户端，这里是为了同步给其他客户端
        bool objectOnServer = isServer;
        //客户端同步到服务端
        bool objectOnClientWithAuthority = (isClientWithAuthority && NetClient.ready);
        if(objectOnServer || objectOnClientWithAuthority)
        {
            if (onlySyncOnChange)
            {
                if (HasChanged(CurrentTransformSnapshot()))
                    SetAllDirty();
            }
            else
                SetAllDirty();

        }
    }
    private void LateUpdate()
    {
        if (updateMode == SyncUpdateMode.LateUpdate)
            UpdateTransform();
    }
    private void UpdateTransform()
    {
        if (isServer) 
            UpdateServer();
        else if (isClient) 
            UpdateClient();
    }
    private void UpdateServer()
    {
        //处理由客户端权威传递的transform快照

        if (syncDirection == SyncDirection.ClientToServer && connectionToClient != null && isOwned == false)
        {
            if (snapshots.Count > 0)
            {
                SnapshotInterpolation.StepInterpolation(snapshots, NetTime.time - serverSnapshotOffset, out TransformSnapshot _, out TransformSnapshot _, out double _);

                int index = snapshots.Count - 1;
                double key = snapshots.Keys[index];

                TransformSnapshot interpolation = snapshots[key];
                if (updateMode == SyncUpdateMode.FixedUpdate)
                    pendingSnapshot = interpolation;//感觉没什么意义
                else
                    Apply(interpolation);
            }
        }
    }
    private void UpdateClient()
    {
        if(isClientWithAuthority == false)
        {
            if (snapshots.Count > 0)
            {
                SnapshotInterpolation.StepInterpolation(
                    snapshots,
                    NetTime.time,
                    out TransformSnapshot from,
                    out TransformSnapshot to,
                    out double t);

                if (updateMode == SyncUpdateMode.FixedUpdate)
                {
                    TransformSnapshot interpolation = TransformSnapshot.Interpolate(from, to, (float)t);
                    pendingSnapshot = interpolation;
                }
                else
                {
                    Apply(from, to, (float)t);
                }
            }
        }
    }
    #endregion
    private bool HasChanged(TransformSnapshot current)
    {
        bool res = false;

        if(syncPosition)
        {
            if (Math.Abs(lastBaselineTransform.position.x - current.position.x) > positionPrecision ||
                Math.Abs(lastBaselineTransform.position.y - current.position.y) > positionPrecision ||
                Math.Abs(lastBaselineTransform.position.z - current.position.z) > positionPrecision)
            res |= true;
        }
        if(syncRotation)
        {
            if (Quaternion.Angle(lastBaselineTransform.rotation, current.rotation) > rotationPrecision)
                res |= true;
        }
            
        if(syncScale)
        {
            if (Math.Abs(lastBaselineTransform.scale.x - current.scale.x) > scalePrecision ||
                Math.Abs(lastBaselineTransform.scale.y - current.scale.y) > scalePrecision ||
                Math.Abs(lastBaselineTransform.scale.z - current.scale.z) > scalePrecision)
                res |= true;
        }

        return res;
    }

    protected TransformSnapshot lastBaselineTransform;

    protected ValueTuple<long, long, long> lastBaselineSerializedPosition;
    protected ValueTuple<long, long, long, long> lastBaselineSerializedRotation;
    protected ValueTuple<long, long, long> lastBaselineSerializedScale;

    protected ValueTuple<long, long, long> lastBaselineDeserializedPosition;
    protected ValueTuple<long, long, long, long> lastBaselineDeserializedRotation;
    protected ValueTuple<long, long, long> lastBaselineDeserializedScale;

    public override void OnSerialize(NetWriter writer, bool isBaseline)
    {
        TransformSnapshot snapshot = CurrentTransformSnapshot();
        if(isBaseline)
        {
            if (syncPosition)
            {
                //后面可以改到write的注册里面
                Compression.ScaleToLong(snapshot.position, out lastBaselineSerializedPosition, positionPrecision);
                writer.Write(lastBaselineSerializedPosition.Item1);
                writer.Write(lastBaselineSerializedPosition.Item2);
                writer.Write(lastBaselineSerializedPosition.Item3);
            }
            
            if (syncRotation)
            {
                Compression.ScaleToLong(snapshot.rotation, out lastBaselineSerializedRotation, rotationPrecision);
                writer.Write(lastBaselineSerializedRotation.Item1);
                writer.Write(lastBaselineSerializedRotation.Item2);
                writer.Write(lastBaselineSerializedRotation.Item3);
                writer.Write(lastBaselineSerializedRotation.Item4);
            }

            if (syncScale)
            {
                Compression.ScaleToLong(snapshot.scale, out lastBaselineSerializedScale, scalePrecision);
                writer.Write(lastBaselineSerializedScale.Item1);
                writer.Write(lastBaselineSerializedScale.Item2);
                writer.Write(lastBaselineSerializedScale.Item3);
            }

            lastBaselineTransform = snapshot;
        }
        else
        {
            if (syncPosition)
            {
                Compression.ScaleToLong(snapshot.position, out ValueTuple<long, long, long> currentPos, positionPrecision);
                long deltaX = currentPos.Item1 - lastBaselineSerializedPosition.Item1;
                long deltaY = currentPos.Item2 - lastBaselineSerializedPosition.Item2;
                long deltaZ = currentPos.Item3 - lastBaselineSerializedPosition.Item3;

                Compression.CompressVarInt(writer, deltaX);
                Compression.CompressVarInt(writer, deltaY);
                Compression.CompressVarInt(writer, deltaZ);
            }
            if (syncRotation)
            {
                Compression.ScaleToLong(snapshot.rotation, out ValueTuple<long, long, long, long> currentRot, rotationPrecision);
                long deltaX = currentRot.Item1 - lastBaselineSerializedRotation.Item1;
                long deltaY = currentRot.Item2 - lastBaselineSerializedRotation.Item2;
                long deltaZ = currentRot.Item3 - lastBaselineSerializedRotation.Item3;
                long deltaW = currentRot.Item4 - lastBaselineSerializedRotation.Item4;

                Compression.CompressVarInt(writer, deltaX);
                Compression.CompressVarInt(writer, deltaY);
                Compression.CompressVarInt(writer, deltaZ);
                Compression.CompressVarInt(writer, deltaW);
            }
            if (syncScale)
            {
                Compression.ScaleToLong(snapshot.scale, out ValueTuple<long, long, long> currentScale, scalePrecision);
                long deltaX = currentScale.Item1 - lastBaselineSerializedScale.Item1;
                long deltaY = currentScale.Item2 - lastBaselineSerializedScale.Item2;
                long deltaZ = currentScale.Item3 - lastBaselineSerializedScale.Item3;

                Compression.CompressVarInt(writer, deltaX);
                Compression.CompressVarInt(writer, deltaY);
                Compression.CompressVarInt(writer, deltaZ);
            }
        }

    }
    public override void OnDeserialize(NetReader reader, bool isBaseline)
    {

        Vector3? _position = null;
        Quaternion? _rotation = null;
        Vector3? _scale = null;

        if (isBaseline)
        {

            if (syncPosition)
            {
                lastBaselineDeserializedPosition.Item1 = reader.Read<long>();
                lastBaselineDeserializedPosition.Item2 = reader.Read<long>();
                lastBaselineDeserializedPosition.Item3 = reader.Read<long>();

                _position = Compression.ScaleToFloat(lastBaselineDeserializedPosition, positionPrecision);

            }

            if (syncRotation)
            {
                lastBaselineDeserializedRotation.Item1 = reader.Read<long>();
                lastBaselineDeserializedRotation.Item2 = reader.Read<long>();
                lastBaselineDeserializedRotation.Item3 = reader.Read<long>();
                lastBaselineDeserializedRotation.Item4 = reader.Read<long>();

                _rotation = Compression.ScaleToFloat(lastBaselineDeserializedRotation, rotationPrecision);
            }

            if (syncScale)
            {
                lastBaselineDeserializedScale.Item1 = reader.Read<long>();
                lastBaselineDeserializedScale.Item2 = reader.Read<long>();
                lastBaselineDeserializedScale.Item3 = reader.Read<long>();

                _scale = Compression.ScaleToFloat(lastBaselineDeserializedScale, scalePrecision);
            }
            if (isServer)
                OnServerDeserialize(_position, _rotation, _scale);
            else
                OnClientDeserialize(_position, _rotation, _scale);
        }
        else
        {
            if (syncPosition)
            {
                long deltaX = Compression.DecompressVarInt(reader);
                long deltaY = Compression.DecompressVarInt(reader);
                long deltaZ = Compression.DecompressVarInt(reader);

                long posXL = lastBaselineDeserializedPosition.Item1 + deltaX;
                long posYL = lastBaselineDeserializedPosition.Item2 + deltaY;
                long posZL = lastBaselineDeserializedPosition.Item3 + deltaZ;
                _position = Compression.ScaleToFloat((posXL, posYL, posZL), positionPrecision);
            }

            if (syncRotation)
            {
                long deltaX = Compression.DecompressVarInt(reader);
                long deltaY = Compression.DecompressVarInt(reader);
                long deltaZ = Compression.DecompressVarInt(reader);
                long deltaW = Compression.DecompressVarInt(reader);

                long rotXL = lastBaselineDeserializedRotation.Item1 + deltaX;
                long rotYL = lastBaselineDeserializedRotation.Item2 + deltaY;
                long rotZL = lastBaselineDeserializedRotation.Item3 + deltaZ;
                long rotWL = lastBaselineDeserializedRotation.Item4 + deltaW;
                _rotation = Compression.ScaleToFloat((rotXL, rotYL, rotZL, rotWL), rotationPrecision);
            }
            if (syncScale)
            {
                long deltaX = Compression.DecompressVarInt(reader);
                long deltaY = Compression.DecompressVarInt(reader);
                long deltaZ = Compression.DecompressVarInt(reader);

                long scaleXL = lastBaselineDeserializedScale.Item1 + deltaX;
                long scaleYL = lastBaselineDeserializedScale.Item2 + deltaY;
                long scaleZL = lastBaselineDeserializedScale.Item3 + deltaZ;
                _scale = Compression.ScaleToFloat((scaleXL, scaleYL, scaleZL), scalePrecision);
            }

            if (isServer)
                OnServerDeserialize(_position, _rotation, _scale);
            else
                OnClientDeserialize(_position, _rotation, _scale);
        }
    }
    //服务端反序列化客户端权威内容
    protected void OnServerDeserialize(Vector3? pos, Quaternion? rot, Vector3? scale)
    {
        if (syncDirection == SyncDirection.ServerToClient) 
            return;

        if (snapshots.Count >= connectionToClient.snapshotBufferSizeLimit) 
            return;

        Debug.Log(pos);
        Debug.Log(rot);
        Debug.Log(scale);

        //服务器端我们不使用插值
        AddSnapshot(snapshots, connectionToClient.remoteTS, connectionToClient.snapshotBufferSizeLimit, pos, rot, scale);
    }
    protected void OnClientDeserialize(Vector3? pos, Quaternion? rot, Vector3? scale)
    {
        if (isClientWithAuthority)
            return;
        if (snapshots.Count >= NetClient.snapshotSettings.bufferLimit)
            return;

        AddSnapshot(snapshots, NetClient.connection.remoteTS + timeStampOffset, NetClient.snapshotSettings.bufferLimit, pos, rot, scale);
    }
}