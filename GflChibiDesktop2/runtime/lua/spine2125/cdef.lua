local ffi = require 'ffi'
ffi.cdef[[
#pragma once
typedef struct spEventData {
	const char* const name;
	int intValue;
	float floatValue;
	const char* stringValue;
} spEventData;
spEventData* spEventData_create (const char* name);
void spEventData_dispose (spEventData* self);
typedef struct spEvent {
	spEventData* const data;
	int intValue;
	float floatValue;
	const char* stringValue;
} spEvent;
spEvent* spEvent_create (spEventData* data);
void spEvent_dispose (spEvent* self);
typedef enum {
	SP_ATTACHMENT_REGION, SP_ATTACHMENT_BOUNDING_BOX, SP_ATTACHMENT_MESH, SP_ATTACHMENT_SKINNED_MESH
} spAttachmentType;
typedef struct spAttachment {
	const char* const name;
	const spAttachmentType type;
	const void* const vtable;
} spAttachment;
void spAttachment_dispose (spAttachment* self);
typedef struct spTimeline spTimeline;
struct spSkeleton;
typedef struct spAnimation {
	const char* const name;
	float duration;
	int timelinesCount;
	spTimeline** timelines;
} spAnimation;
spAnimation* spAnimation_create (const char* name, int timelinesCount);
void spAnimation_dispose (spAnimation* self);
void spAnimation_apply (const spAnimation* self, struct spSkeleton* skeleton, float lastTime, float time, int loop,
		spEvent** events, int* eventsCount);
void spAnimation_mix (const spAnimation* self, struct spSkeleton* skeleton, float lastTime, float time, int loop,
		spEvent** events, int* eventsCount, float alpha);
typedef enum {
	SP_TIMELINE_SCALE,
	SP_TIMELINE_ROTATE,
	SP_TIMELINE_TRANSLATE,
	SP_TIMELINE_COLOR,
	SP_TIMELINE_ATTACHMENT,
	SP_TIMELINE_EVENT,
	SP_TIMELINE_DRAWORDER,
	SP_TIMELINE_FFD,
	SP_TIMELINE_IKCONSTRAINT,
	SP_TIMELINE_FLIPX,
	SP_TIMELINE_FLIPY
} spTimelineType;
struct spTimeline {
	const spTimelineType type;
	const void* const vtable;
};
void spTimeline_dispose (spTimeline* self);
void spTimeline_apply (const spTimeline* self, struct spSkeleton* skeleton, float lastTime, float time, spEvent** firedEvents,
		int* eventsCount, float alpha);
typedef struct spCurveTimeline {
	spTimeline super;
	float* curves; 
} spCurveTimeline;
void spCurveTimeline_setLinear (spCurveTimeline* self, int frameIndex);
void spCurveTimeline_setStepped (spCurveTimeline* self, int frameIndex);
void spCurveTimeline_setCurve (spCurveTimeline* self, int frameIndex, float cx1, float cy1, float cx2, float cy2);
float spCurveTimeline_getCurvePercent (const spCurveTimeline* self, int frameIndex, float percent);
typedef struct spBaseTimeline {
	spCurveTimeline super;
	int const framesCount;
	float* const frames; 
	int boneIndex;
} spBaseTimeline;
typedef struct spBaseTimeline spRotateTimeline;
spRotateTimeline* spRotateTimeline_create (int framesCount);
void spRotateTimeline_setFrame (spRotateTimeline* self, int frameIndex, float time, float angle);
typedef struct spBaseTimeline spTranslateTimeline;
spTranslateTimeline* spTranslateTimeline_create (int framesCount);
void spTranslateTimeline_setFrame (spTranslateTimeline* self, int frameIndex, float time, float x, float y);
typedef struct spBaseTimeline spScaleTimeline;
spScaleTimeline* spScaleTimeline_create (int framesCount);
void spScaleTimeline_setFrame (spScaleTimeline* self, int frameIndex, float time, float x, float y);
typedef struct spColorTimeline {
	spCurveTimeline super;
	int const framesCount;
	float* const frames; 
	int slotIndex;
} spColorTimeline;
spColorTimeline* spColorTimeline_create (int framesCount);
void spColorTimeline_setFrame (spColorTimeline* self, int frameIndex, float time, float r, float g, float b, float a);
typedef struct spAttachmentTimeline {
	spTimeline super;
	int const framesCount;
	float* const frames; 
	int slotIndex;
	const char** const attachmentNames;
} spAttachmentTimeline;
spAttachmentTimeline* spAttachmentTimeline_create (int framesCount);
void spAttachmentTimeline_setFrame (spAttachmentTimeline* self, int frameIndex, float time, const char* attachmentName);
typedef struct spEventTimeline {
	spTimeline super;
	int const framesCount;
	float* const frames; 
	spEvent** const events;
} spEventTimeline;
spEventTimeline* spEventTimeline_create (int framesCount);
void spEventTimeline_setFrame (spEventTimeline* self, int frameIndex, float time, spEvent* event);
typedef struct spDrawOrderTimeline {
	spTimeline super;
	int const framesCount;
	float* const frames; 
	const int** const drawOrders;
	int const slotsCount;
} spDrawOrderTimeline;
spDrawOrderTimeline* spDrawOrderTimeline_create (int framesCount, int slotsCount);
void spDrawOrderTimeline_setFrame (spDrawOrderTimeline* self, int frameIndex, float time, const int* drawOrder);
typedef struct spFFDTimeline {
	spCurveTimeline super;
	int const framesCount;
	float* const frames; 
	int const frameVerticesCount;
	const float** const frameVertices;
	int slotIndex;
	spAttachment* attachment;
} spFFDTimeline;
spFFDTimeline* spFFDTimeline_create (int framesCount, int frameVerticesCount);
void spFFDTimeline_setFrame (spFFDTimeline* self, int frameIndex, float time, float* vertices);
typedef struct spIkConstraintTimeline {
	spCurveTimeline super;
	int const framesCount;
	float* const frames; 
	int ikConstraintIndex;
} spIkConstraintTimeline;
spIkConstraintTimeline* spIkConstraintTimeline_create (int framesCount);
void spIkConstraintTimeline_setFrame (spIkConstraintTimeline* self, int frameIndex, float time, float mix, int bendDirection);
typedef struct spFlipTimeline {
	spTimeline super;
	int const x;
	int const framesCount;
	float* const frames; 
	int boneIndex;
} spFlipTimeline;
spFlipTimeline* spFlipTimeline_create (int framesCount, int x);
void spFlipTimeline_setFrame (spFlipTimeline* self, int frameIndex, float time, int flip);
typedef struct spBoneData spBoneData;
struct spBoneData {
	const char* const name;
	spBoneData* const parent;
	float length;
	float x, y;
	float rotation;
	float scaleX, scaleY;
	int flipX, flipY;
	int inheritScale, inheritRotation;
};
spBoneData* spBoneData_create (const char* name, spBoneData* parent);
void spBoneData_dispose (spBoneData* self);
typedef struct spSlotData {
	const char* const name;
	const spBoneData* const boneData;
	const char* attachmentName;
	float r, g, b, a;
	int additiveBlending;
} spSlotData;
spSlotData* spSlotData_create (const char* name, spBoneData* boneData);
void spSlotData_dispose (spSlotData* self);
void spSlotData_setAttachmentName (spSlotData* self, const char* attachmentName);
struct spSkeleton;
typedef struct spSkin {
	const char* const name;
} spSkin;
spSkin* spSkin_create (const char* name);
void spSkin_dispose (spSkin* self);
void spSkin_addAttachment (spSkin* self, int slotIndex, const char* name, spAttachment* attachment);
spAttachment* spSkin_getAttachment (const spSkin* self, int slotIndex, const char* name);
const char* spSkin_getAttachmentName (const spSkin* self, int slotIndex, int attachmentIndex);
void spSkin_attachAll (const spSkin* self, struct spSkeleton* skeleton, const spSkin* oldspSkin);
typedef struct spIkConstraintData {
	const char* const name;
	
	int bonesCount;
	spBoneData** bones;
	
	spBoneData* target;
	int bendDirection;
	float mix;
} spIkConstraintData;
spIkConstraintData* spIkConstraintData_create (const char* name);
void spIkConstraintData_dispose (spIkConstraintData* self);
typedef struct spSkeletonData {
	const char* version;
	const char* hash;
	float width, height;
	int bonesCount;
	spBoneData** bones;
	int slotsCount;
	spSlotData** slots;
	int skinsCount;
	spSkin** skins;
	spSkin* defaultSkin;
	int eventsCount;
	spEventData** events;
	int animationsCount;
	spAnimation** animations;
	int ikConstraintsCount;
	spIkConstraintData** ikConstraints;
} spSkeletonData;
spSkeletonData* spSkeletonData_create ();
void spSkeletonData_dispose (spSkeletonData* self);
spBoneData* spSkeletonData_findBone (const spSkeletonData* self, const char* boneName);
int spSkeletonData_findBoneIndex (const spSkeletonData* self, const char* boneName);
spSlotData* spSkeletonData_findSlot (const spSkeletonData* self, const char* slotName);
int spSkeletonData_findSlotIndex (const spSkeletonData* self, const char* slotName);
spSkin* spSkeletonData_findSkin (const spSkeletonData* self, const char* skinName);
spEventData* spSkeletonData_findEvent (const spSkeletonData* self, const char* eventName);
spAnimation* spSkeletonData_findAnimation (const spSkeletonData* self, const char* animationName);
spIkConstraintData* spSkeletonData_findIkConstraint (const spSkeletonData* self, const char* ikConstraintName);
typedef struct spAnimationStateData {
	spSkeletonData* const skeletonData;
	float defaultMix;
	const void* const entries;
} spAnimationStateData;
spAnimationStateData* spAnimationStateData_create (spSkeletonData* skeletonData);
void spAnimationStateData_dispose (spAnimationStateData* self);
void spAnimationStateData_setMixByName (spAnimationStateData* self, const char* fromName, const char* toName, float duration);
void spAnimationStateData_setMix (spAnimationStateData* self, spAnimation* from, spAnimation* to, float duration);
float spAnimationStateData_getMix (spAnimationStateData* self, spAnimation* from, spAnimation* to);
typedef enum {
	SP_ANIMATION_START, SP_ANIMATION_END, SP_ANIMATION_COMPLETE, SP_ANIMATION_EVENT
} spEventType;
typedef struct spAnimationState spAnimationState;
typedef void (*spAnimationStateListener) (spAnimationState* state, int trackIndex, spEventType type, spEvent* event,
		int loopCount);
typedef struct spTrackEntry spTrackEntry;
struct spTrackEntry {
	spAnimationState* const state;
	spTrackEntry* next;
	spTrackEntry* previous;
	spAnimation* animation;
	int loop;
	float delay, time, lastTime, endTime, timeScale;
	spAnimationStateListener listener;
	float mixTime, mixDuration, mix;
	void* rendererObject;
};
struct spAnimationState {
	spAnimationStateData* const data;
	float timeScale;
	spAnimationStateListener listener;
	int tracksCount;
	spTrackEntry** tracks;
	void* rendererObject;
	void* userData;
};
spAnimationState* spAnimationState_create (spAnimationStateData* data);
void spAnimationState_dispose (spAnimationState* self);
void spAnimationState_update (spAnimationState* self, float delta);
void spAnimationState_apply (spAnimationState* self, struct spSkeleton* skeleton);
void spAnimationState_clearTracks (spAnimationState* self);
void spAnimationState_clearTrack (spAnimationState* self, int trackIndex);
spTrackEntry* spAnimationState_setAnimationByName (spAnimationState* self, int trackIndex, const char* animationName,
		int loop);
spTrackEntry* spAnimationState_setAnimation (spAnimationState* self, int trackIndex, spAnimation* animation, int loop);
spTrackEntry* spAnimationState_addAnimationByName (spAnimationState* self, int trackIndex, const char* animationName,
		int loop, float delay);
spTrackEntry* spAnimationState_addAnimation (spAnimationState* self, int trackIndex, spAnimation* animation, int loop,
		float delay);
spTrackEntry* spAnimationState_getCurrent (spAnimationState* self, int trackIndex);
typedef struct spAtlas spAtlas;
typedef enum {
	SP_ATLAS_ALPHA,
	SP_ATLAS_INTENSITY,
	SP_ATLAS_LUMINANCE_ALPHA,
	SP_ATLAS_RGB565,
	SP_ATLAS_RGBA4444,
	SP_ATLAS_RGB888,
	SP_ATLAS_RGBA8888
} spAtlasFormat;
typedef enum {
	SP_ATLAS_NEAREST,
	SP_ATLAS_LINEAR,
	SP_ATLAS_MIPMAP,
	SP_ATLAS_MIPMAP_NEAREST_NEAREST,
	SP_ATLAS_MIPMAP_LINEAR_NEAREST,
	SP_ATLAS_MIPMAP_NEAREST_LINEAR,
	SP_ATLAS_MIPMAP_LINEAR_LINEAR
} spAtlasFilter;
typedef enum {
	SP_ATLAS_MIRROREDREPEAT, SP_ATLAS_CLAMPTOEDGE, SP_ATLAS_REPEAT
} spAtlasWrap;
typedef struct spAtlasPage spAtlasPage;
struct spAtlasPage {
	const spAtlas* atlas;
	const char* name;
	spAtlasFormat format;
	spAtlasFilter minFilter, magFilter;
	spAtlasWrap uWrap, vWrap;
	void* rendererObject;
	int width, height;
	spAtlasPage* next;
};
spAtlasPage* spAtlasPage_create (spAtlas* atlas, const char* name);
void spAtlasPage_dispose (spAtlasPage* self);
typedef struct spAtlasRegion spAtlasRegion;
struct spAtlasRegion {
	const char* name;
	int x, y, width, height;
	float u, v, u2, v2;
	int offsetX, offsetY;
	int originalWidth, originalHeight;
	int index;
	int rotate;
	int flip;
	int* splits;
	int* pads;
	spAtlasPage* page;
	spAtlasRegion* next;
};
spAtlasRegion* spAtlasRegion_create ();
void spAtlasRegion_dispose (spAtlasRegion* self);
struct spAtlas {
	spAtlasPage* pages;
	spAtlasRegion* regions;
	void* rendererObject;
};
spAtlas* spAtlas_create (const char* data, int length, const char* dir, void* rendererObject);
spAtlas* spAtlas_createFromFile (const char* path, void* rendererObject);
void spAtlas_dispose (spAtlas* atlas);
spAtlasRegion* spAtlas_findRegion (const spAtlas* self, const char* name);
typedef struct spAttachmentLoader {
	const char* error1;
	const char* error2;
	const void* const vtable;
} spAttachmentLoader;
void spAttachmentLoader_dispose (spAttachmentLoader* self);
spAttachment* spAttachmentLoader_newAttachment (spAttachmentLoader* self, spSkin* skin, spAttachmentType type, const char* name,
		const char* path);
typedef struct spAtlasAttachmentLoader {
	spAttachmentLoader super;
	spAtlas* atlas;
} spAtlasAttachmentLoader;
spAtlasAttachmentLoader* spAtlasAttachmentLoader_create (spAtlas* atlas);
struct spSkeleton;
typedef struct spBone spBone;
struct spBone {
	spBoneData* const data;
	struct spSkeleton* const skeleton;
	spBone* const parent;
	float x, y;
	float rotation, rotationIK;
	float scaleX, scaleY;
	int flipX, flipY;
	float const m00, m01, worldX; 
	float const m10, m11, worldY; 
	float const worldRotation;
	float const worldScaleX, worldScaleY;
	int const worldFlipX, worldFlipY;
};
void spBone_setYDown (int yDown);
int spBone_isYDown ();
spBone* spBone_create (spBoneData* data, struct spSkeleton* skeleton, spBone* parent);
void spBone_dispose (spBone* self);
void spBone_setToSetupPose (spBone* self);
void spBone_updateWorldTransform (spBone* self);
void spBone_worldToLocal (spBone* self, float worldX, float worldY, float* localX, float* localY);
void spBone_localToWorld (spBone* self, float localX, float localY, float* worldX, float* worldY);
typedef struct spSlot {
	spSlotData* const data;
	spBone* const bone;
	float r, g, b, a;
	spAttachment* const attachment;
	int attachmentVerticesCapacity;
	int attachmentVerticesCount;
	float* attachmentVertices;
} spSlot;
spSlot* spSlot_create (spSlotData* data, spBone* bone);
void spSlot_dispose (spSlot* self);
void spSlot_setAttachment (spSlot* self, spAttachment* attachment);
void spSlot_setAttachmentTime (spSlot* self, float time);
float spSlot_getAttachmentTime (const spSlot* self);
void spSlot_setToSetupPose (spSlot* self);
typedef enum {
	SP_VERTEX_X1 = 0, SP_VERTEX_Y1, SP_VERTEX_X2, SP_VERTEX_Y2, SP_VERTEX_X3, SP_VERTEX_Y3, SP_VERTEX_X4, SP_VERTEX_Y4
} spVertexIndex;
typedef struct spRegionAttachment {
	spAttachment super;
	const char* path;
	float x, y, scaleX, scaleY, rotation, width, height;
	float r, g, b, a;
	void* rendererObject;
	int regionOffsetX, regionOffsetY; 
	int regionWidth, regionHeight; 
	int regionOriginalWidth, regionOriginalHeight; 
	float offset[8];
	float uvs[8];
} spRegionAttachment;
spRegionAttachment* spRegionAttachment_create (const char* name);
void spRegionAttachment_setUVs (spRegionAttachment* self, float u, float v, float u2, float v2, int rotate);
void spRegionAttachment_updateOffset (spRegionAttachment* self);
void spRegionAttachment_computeWorldVertices (spRegionAttachment* self, spBone* bone, float* vertices);
typedef struct spMeshAttachment {
	spAttachment super;
	const char* path;
	int verticesCount;
	float* vertices;
	int hullLength;
	float* regionUVs;
	float* uvs;
	int trianglesCount;
	int* triangles;
	float r, g, b, a;
	void* rendererObject;
	int regionOffsetX, regionOffsetY; 
	int regionWidth, regionHeight; 
	int regionOriginalWidth, regionOriginalHeight; 
	float regionU, regionV, regionU2, regionV2;
	int regionRotate;
	
	int edgesCount;
	int* edges;
	float width, height;
} spMeshAttachment;
spMeshAttachment* spMeshAttachment_create (const char* name);
void spMeshAttachment_updateUVs (spMeshAttachment* self);
void spMeshAttachment_computeWorldVertices (spMeshAttachment* self, spSlot* slot, float* worldVertices);
typedef struct spSkinnedMeshAttachment {
	spAttachment super;
	const char* path;
	int bonesCount;
	int* bones;
	int weightsCount;
	float* weights;
	int trianglesCount;
	int* triangles;
	int uvsCount;
	float* regionUVs;
	float* uvs;
	int hullLength;
	float r, g, b, a;
	void* rendererObject;
	int regionOffsetX, regionOffsetY; 
	int regionWidth, regionHeight; 
	int regionOriginalWidth, regionOriginalHeight; 
	float regionU, regionV, regionU2, regionV2;
	int regionRotate;
	
	int edgesCount;
	int* edges;
	float width, height;
} spSkinnedMeshAttachment;
spSkinnedMeshAttachment* spSkinnedMeshAttachment_create (const char* name);
void spSkinnedMeshAttachment_updateUVs (spSkinnedMeshAttachment* self);
void spSkinnedMeshAttachment_computeWorldVertices (spSkinnedMeshAttachment* self, spSlot* slot, float* worldVertices);
typedef struct spBoundingBoxAttachment {
	spAttachment super;
	int verticesCount;
	float* vertices;
} spBoundingBoxAttachment;
spBoundingBoxAttachment* spBoundingBoxAttachment_create (const char* name);
void spBoundingBoxAttachment_computeWorldVertices (spBoundingBoxAttachment* self, spBone* bone, float* vertices);
struct spSkeleton;
typedef struct spIkConstraint {
	spIkConstraintData* const data;
	
	int bonesCount;
	spBone** bones;
	
	spBone* target;
	int bendDirection;
	float mix;
} spIkConstraint;
spIkConstraint* spIkConstraint_create (spIkConstraintData* data, const struct spSkeleton* skeleton);
void spIkConstraint_dispose (spIkConstraint* self);
void spIkConstraint_apply (spIkConstraint* self);
void spIkConstraint_apply1 (spBone* bone, float targetX, float targetY, float alpha);
void spIkConstraint_apply2 (spBone* parent, spBone* child, float targetX, float targetY, int bendDirection, float alpha);
typedef struct spSkeleton {
	spSkeletonData* const data;
	int bonesCount;
	spBone** bones;
	spBone* const root;
	int slotsCount;
	spSlot** slots;
	spSlot** drawOrder;
	int ikConstraintsCount;
	spIkConstraint** ikConstraints;
	spSkin* const skin;
	float r, g, b, a;
	float time;
	int flipX, flipY;
	float x, y;
} spSkeleton;
spSkeleton* spSkeleton_create (spSkeletonData* data);
void spSkeleton_dispose (spSkeleton* self);
void spSkeleton_updateCache (const spSkeleton* self);
void spSkeleton_updateWorldTransform (const spSkeleton* self);
void spSkeleton_setToSetupPose (const spSkeleton* self);
void spSkeleton_setBonesToSetupPose (const spSkeleton* self);
void spSkeleton_setSlotsToSetupPose (const spSkeleton* self);
spBone* spSkeleton_findBone (const spSkeleton* self, const char* boneName);
int spSkeleton_findBoneIndex (const spSkeleton* self, const char* boneName);
spSlot* spSkeleton_findSlot (const spSkeleton* self, const char* slotName);
int spSkeleton_findSlotIndex (const spSkeleton* self, const char* slotName);
void spSkeleton_setSkin (spSkeleton* self, spSkin* skin);
int spSkeleton_setSkinByName (spSkeleton* self, const char* skinName);
spAttachment* spSkeleton_getAttachmentForSlotName (const spSkeleton* self, const char* slotName, const char* attachmentName);
spAttachment* spSkeleton_getAttachmentForSlotIndex (const spSkeleton* self, int slotIndex, const char* attachmentName);
int spSkeleton_setAttachment (spSkeleton* self, const char* slotName, const char* attachmentName);
spIkConstraint* spSkeleton_findIkConstraint (const spSkeleton* self, const char* ikConstraintName);
void spSkeleton_update (spSkeleton* self, float deltaTime);
typedef struct spPolygon {
	float* const vertices;
	int count;
	int capacity;
} spPolygon;
spPolygon* spPolygon_create (int capacity);
void spPolygon_dispose (spPolygon* self);
int spPolygon_containsPoint (spPolygon* polygon, float x, float y);
int spPolygon_intersectsSegment (spPolygon* polygon, float x1, float y1, float x2, float y2);
typedef struct spSkeletonBounds {
	int count;
	spBoundingBoxAttachment** boundingBoxes;
	spPolygon** polygons;
	float minX, minY, maxX, maxY;
} spSkeletonBounds;
spSkeletonBounds* spSkeletonBounds_create ();
void spSkeletonBounds_dispose (spSkeletonBounds* self);
void spSkeletonBounds_update (spSkeletonBounds* self, spSkeleton* skeleton, int updateAabb);
int spSkeletonBounds_aabbContainsPoint (spSkeletonBounds* self, float x, float y);
int spSkeletonBounds_aabbIntersectsSegment (spSkeletonBounds* self, float x1, float y1, float x2, float y2);
int spSkeletonBounds_aabbIntersectsSkeleton (spSkeletonBounds* self, spSkeletonBounds* bounds);
spBoundingBoxAttachment* spSkeletonBounds_containsPoint (spSkeletonBounds* self, float x, float y);
spBoundingBoxAttachment* spSkeletonBounds_intersectsSegment (spSkeletonBounds* self, float x1, float y1, float x2, float y2);
spPolygon* spSkeletonBounds_getPolygon (spSkeletonBounds* self, spBoundingBoxAttachment* boundingBox);
typedef struct spSkeletonJson {
	float scale;
	spAttachmentLoader* attachmentLoader;
	const char* const error;
} spSkeletonJson;
spSkeletonJson* spSkeletonJson_createWithLoader (spAttachmentLoader* attachmentLoader);
spSkeletonJson* spSkeletonJson_create (spAtlas* atlas);
void spSkeletonJson_dispose (spSkeletonJson* self);
spSkeletonData* spSkeletonJson_readSkeletonData (spSkeletonJson* self, const char* json);
spSkeletonData* spSkeletonJson_readSkeletonDataFile (spSkeletonJson* self, const char* path);
typedef struct spSkeletonBinary {
	float scale;
	spAttachmentLoader* attachmentLoader;
	const char* const error;
} spSkeletonBinary;
spSkeletonBinary* spSkeletonBinary_createWithLoader(spAttachmentLoader* attachmentLoader);
spSkeletonBinary* spSkeletonBinary_create(spAtlas* atlas);
void spSkeletonBinary_dispose(spSkeletonBinary* self);
spSkeletonData* spSkeletonBinary_readSkeletonData(spSkeletonBinary* self, const unsigned char* binary, const int length);
spSkeletonData* spSkeletonBinary_readSkeletonDataFile(spSkeletonBinary* self, const char* path);
typedef struct HitTestRecorder_t {
	uint64_t count;
	void** list;
	uint64_t capacity;
} HitTestRecorder;
typedef struct eventRecorderAtom_t {
	spEventType type;
	spTrackEntry* entry;
	spEvent event;
	struct eventRecorderAtom_t* next;
} eventRecorderAtom;
void drawSkeleton(spSkeleton* skeleton, bool PMA);
int spSkeleton_containsPoint(spSkeleton* self, float px, float py, HitTestRecorder* re);
void spSkeleton_getAabbBox(spSkeleton* self, Rectangle* rect);
void eventListenerFunc(spAnimationState* state, int trackIndex, spEventType type, spEvent* event, int loopCount);
void releaseAllEvents(spAnimationState* state);
]]
return hdtLoadFFI('hdt-raylib-spine.dll')
