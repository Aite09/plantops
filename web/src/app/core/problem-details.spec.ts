import { HttpErrorResponse } from '@angular/common/http';
import { FormControl, FormGroup } from '@angular/forms';
import { applyServerErrors, problemMessage, toProblem } from './problem-details';

describe('toProblem', () => {
  it('turns a status 0 network failure into a connection message', () => {
    // HttpClient reports an unreachable server as status 0 with the browser's ProgressEvent as the body.
    const error = new HttpErrorResponse({ status: 0, statusText: 'Unknown Error', error: new ProgressEvent('error') });

    expect(toProblem(error)).toEqual({
      status: 0,
      title: 'Cannot reach the PlantOps server. Check your connection and try again.',
    });
  });

  it('keeps the fields of a ProblemDetails body, including the machine-readable code', () => {
    const body = {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.10',
      title: 'Conflict',
      status: 409,
      detail: 'Cannot move a work order from Open to InProgress.',
      code: 'WorkOrder.InvalidTransition',
    };
    const error = new HttpErrorResponse({ status: 409, statusText: 'Conflict', error: body });

    expect(toProblem(error)).toEqual(body);
  });

  it('keeps the per-field errors of a validation problem, which the forms show under each field', () => {
    const body = {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: { title: ['Title is required.'], equipmentId: ['Choose the equipment.'] },
      code: 'Validation.Failed',
    };
    const error = new HttpErrorResponse({ status: 400, statusText: 'Bad Request', error: body });

    expect(toProblem(error).errors).toEqual(body.errors);
  });

  it('takes the status from the response when the body has none', () => {
    const error = new HttpErrorResponse({ status: 400, error: { title: 'One or more validation errors occurred.' } });

    expect(toProblem(error)).toEqual({ status: 400, title: 'One or more validation errors occurred.' });
  });

  it('uses the status text when the body is not JSON', () => {
    // e.g. a reverse proxy's HTML error page in front of the API
    const error = new HttpErrorResponse({
      status: 502,
      statusText: 'Bad Gateway',
      error: '<html>502 Bad Gateway</html>',
    });

    expect(toProblem(error)).toEqual({ status: 502, title: 'Bad Gateway' });
  });

  it('falls back to a generic title for errors that are not HTTP responses', () => {
    expect(toProblem(new TypeError('x is undefined'))).toEqual({ title: 'Something went wrong.' });
  });
});

describe('problemMessage', () => {
  it('asks the user to wait on 429 Too Many Requests, whatever the body says', () => {
    expect(problemMessage({ status: 429, title: 'Too Many Requests', detail: 'Rate limit exceeded.' })).toBe(
      'Too many attempts. Wait a minute and try again.',
    );
  });

  it('prefers the specific detail over the title', () => {
    expect(problemMessage({ status: 409, title: 'Conflict', detail: 'The work order is already closed.' })).toBe(
      'The work order is already closed.',
    );
  });

  it('uses the title when there is no detail', () => {
    expect(problemMessage({ status: 403, title: 'Forbidden' })).toBe('Forbidden');
  });

  it('falls back to a generic message when there is neither', () => {
    expect(problemMessage({ status: 500 })).toBe('Something went wrong.');
  });
});

describe('applyServerErrors', () => {
  function userForm(): FormGroup {
    return new FormGroup({
      email: new FormControl(''),
      password: new FormControl(''),
      displayName: new FormControl(''),
    });
  }

  it('puts all messages for a field on the matching control as one joined error and marks it touched', () => {
    const form = userForm();

    applyServerErrors(form, {
      status: 400,
      errors: { password: ['Passwords must be at least 10 characters.', 'Passwords must have at least one digit.'] },
    });

    const password = form.controls['password'];
    expect(password.errors).toEqual({
      server: 'Passwords must be at least 10 characters. Passwords must have at least one digit.',
    });
    expect(password.touched).toBe(true);
  });

  it('leaves controls without server messages untouched', () => {
    const form = userForm();

    applyServerErrors(form, { status: 400, errors: { email: ['Email is already taken.'] } });

    const displayName = form.controls['displayName'];
    expect(displayName.errors).toBeNull();
    expect(displayName.touched).toBe(false);
  });

  it('returns the messages that match no control so they can be shown above the form', () => {
    const form = userForm();

    const unmatched = applyServerErrors(form, {
      status: 400,
      errors: {
        email: ['Email is already taken.'],
        roles: ['Choose at least one role.', 'Unknown role "Guest".'],
      },
    });

    expect(unmatched).toEqual(['Choose at least one role.', 'Unknown role "Guest".']);
    expect(form.controls['email'].errors).toEqual({ server: 'Email is already taken.' });
  });

  it('returns nothing and changes nothing when the problem has no field errors', () => {
    const form = userForm();

    const unmatched = applyServerErrors(form, { status: 409, title: 'Conflict' });

    expect(unmatched).toEqual([]);
    expect(form.valid).toBe(true);
    expect(form.touched).toBe(false);
  });
});
