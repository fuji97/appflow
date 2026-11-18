using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AppFlow.Editor
{
    /// <summary>Scene picker for <see cref="SceneReference"/>: stores the GUID and the current path.</summary>
    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var guid = property.FindPropertyRelative(nameof(SceneReference.Guid));
            var path = property.FindPropertyRelative(nameof(SceneReference.Path));
            var field = new ObjectField(property.displayName) { objectType = typeof(SceneAsset), allowSceneObjects = false };
            field.AddToClassList(ObjectField.alignedFieldUssClassName);
            if (!string.IsNullOrEmpty(guid.stringValue))
                field.SetValueWithoutNotify(AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(guid.stringValue)));
            field.RegisterValueChangedCallback(e =>
            {
                var p = e.newValue != null ? AssetDatabase.GetAssetPath(e.newValue) : "";
                guid.stringValue = p == "" ? "" : AssetDatabase.AssetPathToGUID(p);
                path.stringValue = p;
                property.serializedObject.ApplyModifiedProperties();
            });
            return field;
        }
    }
}
