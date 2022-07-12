// Scintilla source code edit control
/** @file EditModel.h
 ** Defines the editor state that must be visible to EditorView.
 **/
// Copyright 1998-2014 by Neil Hodgson <neilh@scintilla.org>
// The License.txt file describes the conditions under which this software may be distributed.

#ifndef EDITMODEL_H
#define EDITMODEL_H

namespace Scintilla::Internal {

/**
*/
class Caret {
public:
	bool active;
	bool on;
	int period;

	Caret() noexcept;
};

class EditModel {
public:
	bool inOverstrike;
	int xOffset;		///< Horizontal scrolled amount in pixels
	bool trackLineWidth;

	SpecialRepresentations reprs;
	Caret caret;
	SelectionPosition posDrag;
	Sci::Position braces[2];
	int bracesMatchStyle;
	int highlightGuideColumn;
	bool hasFocus;
	Selection sel;
	bool primarySelection;

	Scintilla::IMEInteraction imeInteraction;
	Scintilla::Bidirectional bidirectional;

	Scintilla::FoldFlag foldFlags;
	Scintilla::FoldDisplayTextStyle foldDisplayTextStyle;
	UniqueString defaultFoldDisplayText;
	std::unique_ptr<IContractionState> pcs;
	// Hotspot support
	Range hotspot;
	bool hotspotSingleLine;
	Sci::Position hoverIndicatorPos;

	// Wrapping support
	int wrapWidth;

	Document *pdoc;

	//Au
	Sci_NotifyCallback cbNotify; void* cbNotifyParam; //can use callback function instead of WM_NOTIFY.
	Sci_AnnotationDrawCallback cbAnnotationDraw; void* cbAnnotationDrawParam; //to draw in annotation eg image instead of text.
	Sci_MarginDrawCallback cbMarginDraw; int cbMarginsMask; //to draw eg images in margin.

	EditModel();
	// Deleted so EditModel objects can not be copied.
	EditModel(const EditModel &) = delete;
	EditModel(EditModel &&) = delete;
	EditModel &operator=(const EditModel &) = delete;
	EditModel &operator=(EditModel &&) = delete;
	virtual ~EditModel();
	virtual Sci::Line TopLineOfMain() const = 0;
	virtual Point GetVisibleOriginInMain() const = 0;
	virtual Sci::Line LinesOnScreen() const = 0;
	bool BidirectionalEnabled() const noexcept;
	bool BidirectionalR2L() const noexcept;
	void SetDefaultFoldDisplayText(const char *text);
	const char *GetDefaultFoldDisplayText() const noexcept;
	const char *GetFoldDisplayText(Sci::Line lineDoc) const noexcept;
	InSelection LineEndInSelection(Sci::Line lineDoc) const;
};

}

#endif