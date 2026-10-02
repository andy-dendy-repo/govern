// Helpers shared by the board's layers: many copies of one shape as a single InstancedMesh, and freeing what a layer built.
import { InstancedMesh, Matrix4, Quaternion, Vector3 } from '../../lib/three/three.module.js';

const up = new Vector3(0, 1, 0);

// Many copies of one shape, each with its own place, size, rotation (`rotation`, or a turn about the vertical) and colour.
export function instances(geometry, material, items) {
    const mesh = new InstancedMesh(geometry, material, Math.max(items.length, 1));
    mesh.count = items.length;
    const matrix = new Matrix4();
    const turn = new Quaternion();
    items.forEach((item, i) => {
        const rotation = item.rotation ?? turn.setFromAxisAngle(up, item.turn ?? 0);
        matrix.compose(new Vector3(item.x, item.y, item.z), rotation, new Vector3(item.sx ?? 1, item.sy ?? 1, item.sz ?? 1));
        mesh.setMatrixAt(i, matrix);
        mesh.setColorAt(i, item.colour);
    });
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    return mesh;
}

// Frees the geometry and instance buffers a layer created (its shared shapes and materials stay).
export function disposeLayer(group) {
    group.traverse(object => {
        if (object.isInstancedMesh) {
            object.dispose();
        } else if (object.isMesh) {
            object.geometry.dispose();
        }
    });
}
