import { useId } from 'react'
import { DataCredit } from '../DataCredit'
import { LineSwatch } from './LineSwatch'
import {
  BallotIcon,
  CalendarIcon,
  CheckIcon,
  ChevronIcon,
  PeopleIcon,
} from '../icons'
import {
  countOf,
  coverageTitle,
  districtName,
  formatElectionDate,
} from '../../districts/labels'
import {
  layerRole,
  type LayerRole,
  type MapLayers,
} from '../../districts/selection'
import type { District, ElectionSummary } from '../../districts/types'
import './DistrictRail.css'

// Every district that covers the address, as a radio group: one Tab stop,
// arrow keys to move, and the browser announcing "3 of 6" with no custom
// keyboard code. On a phone the same list becomes a sheet under the map.

type DistrictRailProps = {
  districts: readonly District[]
  layers: MapLayers
  election: ElectionSummary | null
  /** What the boundaries were drawn from, named in the corner. */
  source: string | null
  /** The phone layout: a sheet with a handle, a hint, and a one-line footer. */
  compact: boolean
  sheetExpanded: boolean
  onToggleSheet: () => void
  onSelect: (districtId: string) => void
}

export function DistrictRail({
  districts,
  layers,
  election,
  source,
  compact,
  sheetExpanded,
  onToggleSheet,
  onSelect,
}: DistrictRailProps) {
  const baseId = useId()
  const titleId = `${baseId}-title`
  const listId = `${baseId}-list`
  // Districts get redrawn, so say which data these came from.
  const sourceTag =
    source === null ? null : <span className="source-tag">{source}</span>

  let footer = null

  if (election !== null) {
    footer = compact ? (
      <ElectionStrip election={election} />
    ) : (
      <ElectionFooter election={election} />
    )
  }

  return (
    <section className="district-rail" aria-labelledby={titleId}>
      {compact ? (
        <button
          type="button"
          className="district-rail__handle"
          aria-expanded={sheetExpanded}
          aria-controls={listId}
          onClick={onToggleSheet}
        >
          <span className="visually-hidden">District list</span>
        </button>
      ) : null}

      <div className="district-rail__head">
        {compact ? null : (
          <p className="district-rail__eyebrow">
            <span>Your maps</span>
            {sourceTag}
          </p>
        )}
        <div className="district-rail__title-row">
          <h2 id={titleId} className="district-rail__title">
            {coverageTitle(districts.length)}
          </h2>
          {compact ? (
            <p className="district-rail__hint">
              {sourceTag}
              <span>Tap to draw</span>
            </p>
          ) : null}
        </div>
        {compact ? null : (
          <p className="district-rail__lede">
            Select one to draw its boundary on the map.
          </p>
        )}
      </div>

      <div
        id={listId}
        className="district-rail__list"
        role="radiogroup"
        aria-labelledby={titleId}
      >
        {districts.map((district) => (
          <DistrictRow
            key={district.id}
            district={district}
            role={layerRole(layers, district)}
            groupName={baseId}
            idPrefix={`${baseId}-${district.id}`}
            swatchWidth={compact ? 22 : 26}
            onSelect={onSelect}
          />
        ))}
      </div>

      <DataCredit className="district-rail__credit" />

      {footer}
    </section>
  )
}

type DistrictRowProps = {
  district: District
  role: LayerRole
  groupName: string
  idPrefix: string
  swatchWidth: number
  onSelect: (districtId: string) => void
}

function DistrictRow({
  district,
  role,
  groupName,
  idPrefix,
  swatchWidth,
  onSelect,
}: DistrictRowProps) {
  const selected = role === 'selected'
  const nameId = `${idPrefix}-name`
  const detailId = `${idPrefix}-detail`

  return (
    <label
      className={selected ? 'district-row district-row--selected' : 'district-row'}
    >
      <input
        type="radio"
        className="visually-hidden"
        name={groupName}
        value={district.id}
        checked={selected}
        onChange={() => onSelect(district.id)}
        aria-labelledby={nameId}
        aria-describedby={detailId}
      />
      <span className="district-row__body">
        <LineSwatch
          role={role}
          tone={selected ? 'accent' : 'muted'}
          width={swatchWidth}
        />
        <span className="district-row__text">
          <span id={nameId} className="district-row__name">
            {districtName(district)}
          </span>
          <span id={detailId} className="district-row__detail">
            {selected ? 'Shown on the map' : district.detail}
          </span>
        </span>
        {selected ? (
          <CheckIcon className="district-row__icon" />
        ) : (
          <ChevronIcon className="district-row__icon" />
        )}
      </span>
    </label>
  )
}

function ElectionFooter({ election }: { election: ElectionSummary }) {
  return (
    <div className="district-rail__foot">
      <p className="district-rail__foot-title">Also for this address</p>
      <ul className="district-rail__facts">
        <li className="district-rail__fact">
          <CalendarIcon />
          <span>
            Next election · <strong>{formatElectionDate(election.date)}</strong>
          </span>
        </li>
        {election.candidates === null ? null : (
          <li className="district-rail__fact">
            <PeopleIcon />
            <span>
              <strong>
                {countOf(election.candidates, 'candidate', 'candidates')}
              </strong>{' '}
              on your ballot
            </span>
          </li>
        )}
        {election.measures === null ? null : (
          <li className="district-rail__fact">
            <BallotIcon />
            <strong>
              {countOf(election.measures, 'ballot measure', 'ballot measures')}
            </strong>
          </li>
        )}
      </ul>
    </div>
  )
}

function ElectionStrip({ election }: { election: ElectionSummary }) {
  return (
    <p className="district-rail__strip">
      <CalendarIcon />
      <span>
        Next election <strong>{formatElectionDate(election.date)}</strong>
        {election.candidates === null
          ? null
          : ` · ${countOf(election.candidates, 'candidate', 'candidates')}`}
      </span>
    </p>
  )
}

