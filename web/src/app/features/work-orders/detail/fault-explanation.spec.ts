import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ExplanationState, FaultExplanationPanel } from './fault-explanation';

describe('FaultExplanationPanel', () => {
  let fixture: ComponentFixture<FaultExplanationPanel>;
  let requests: number;

  async function render(state: ExplanationState): Promise<HTMLElement> {
    TestBed.configureTestingModule({ imports: [FaultExplanationPanel] });
    fixture = TestBed.createComponent(FaultExplanationPanel);
    requests = 0;
    fixture.componentInstance.explain.subscribe(() => requests++);
    fixture.componentRef.setInput('state', state);
    await fixture.whenStable();
    return fixture.nativeElement as HTMLElement;
  }

  function button(panel: HTMLElement): HTMLButtonElement {
    return panel.querySelector('button') as HTMLButtonElement;
  }

  it('starts with one button that asks for an explanation', async () => {
    const panel = await render({ status: 'idle' });

    button(panel).click();

    expect(button(panel).textContent?.trim()).toBe('Explain this fault');
    expect(requests).toBe(1);
  });

  it('while waiting keeps the button focusable but ignores further presses', async () => {
    const panel = await render({ status: 'loading' });

    button(panel).click();

    expect(button(panel).textContent?.trim()).toBe('Explaining…');
    expect(button(panel).getAttribute('aria-disabled')).toBe('true');
    expect(requests).toBe(0);
    // Announced while waiting: inside the polite live region, which is never marked busy (that would mute it).
    const live = panel.querySelector('[aria-live="polite"]');
    expect(live?.textContent).toContain('Generating suggestions. This can take up to a minute.');
    expect(live?.hasAttribute('aria-busy')).toBe(false);
  });

  it('shows the answer as plain text, never as HTML, after the warning that nobody has checked it', async () => {
    const panel = await render({
      status: 'loaded',
      data: { text: 'Check the <b>seal</b> first.', model: 'phi4-mini', generatedAtUtc: '2026-03-16T06:00:00Z' },
    });

    const text = panel.querySelector('.text') as HTMLElement;
    expect(text.textContent).toBe('Check the <b>seal</b> first.');
    expect(text.querySelector('b')).toBeNull();
    const caveat = panel.querySelector('.caveat') as HTMLElement;
    expect(caveat.textContent).toContain('AI-generated and not checked by anyone');
    expect(caveat.compareDocumentPosition(text) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(panel.querySelector('h2')?.textContent).toBe('AI troubleshooting suggestions');
    expect(panel.querySelector('.provenance')?.textContent).toContain('phi4-mini');
    expect(button(panel).textContent?.trim()).toBe('Explain again');
  });

  it('says what went wrong and offers to try again', async () => {
    const panel = await render({
      status: 'error',
      problem: { status: 503, code: 'FaultExplanation.Unavailable', detail: 'The AI model did not answer.' },
    });

    expect(panel.querySelector('[role="alert"]')?.textContent).toContain('The AI model did not answer.');
    expect(button(panel).textContent?.trim()).toBe('Try again');
  });

  it('offers no button once the API says the feature is switched off', async () => {
    const panel = await render({
      status: 'error',
      problem: { status: 404, code: 'FaultExplanation.Disabled', detail: 'Explaining faults is switched off.' },
    });

    expect(panel.querySelector('[role="alert"]')?.textContent).toContain('switched off');
    expect(panel.querySelector('button')).toBeNull();
  });
});
